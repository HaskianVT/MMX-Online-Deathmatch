using MMXOnline;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RelayServer
{
    // Accepts WebSocket connections on localhost (forwarded by cloudflared from the public domain) and bridges each one
    // to the relay's own TCP matchmaking port or to one of its UDP ports. See MMXOnline.WebSocketTunnel for the client side.
    public static class WebSocketGateway
    {
        const string webSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        const int maxHeaderBytes = 16 * 1024;
        const int udpIdleTimeoutMs = 60000;

        public static void start()
        {
            var listener = new TcpListener(IPAddress.Loopback, WebSocketTunnel.gatewayPort);
            listener.Start();
            Task.Run(async () =>
            {
                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => handleConnection(client));
                }
            });
        }

        class HttpRequest
        {
            public string path;
            public Dictionary<string, string> query = new Dictionary<string, string>();
            public Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public string getHeader(string name) => headers.TryGetValue(name, out string value) ? value : null;
            public string getQuery(string name) => query.TryGetValue(name, out string value) ? value : null;
        }

        static async Task handleConnection(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.NoDelay = true;
                    NetworkStream stream = client.GetStream();

                    HttpRequest request = readHttpRequest(client, stream);
                    if (request == null) return;

                    // cloudflared passes the player's real IP in this header. Only cloudflared (or this machine) can reach the gateway, so it can be trusted.
                    string clientIp = request.getHeader("CF-Connecting-IP")?.Trim();
                    if (string.IsNullOrEmpty(clientIp) || !IPAddress.TryParse(clientIp, out _))
                    {
                        clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                    }

                    if (!request.headers.TryGetValue("Sec-WebSocket-Key", out string webSocketKey) ||
                        !string.Equals(request.getHeader("Upgrade"), "websocket", StringComparison.OrdinalIgnoreCase))
                    {
                        // Plain HTTP request, e.g. opening the domain in a browser to check the tunnel works
                        writeHttpResponse(stream, "200 OK", "MMXOD relay server WebSocket gateway is running.");
                        return;
                    }

                    int? udpPort = null;
                    if (request.path.EndsWith("/udp"))
                    {
                        if (!int.TryParse(request.getQuery("port"), out int port) ||
                            port < Global.basePort || port > Global.basePort + WebSocketTunnel.maxUdpPortOffset)
                        {
                            writeHttpResponse(stream, "400 Bad Request", "Invalid UDP port.");
                            return;
                        }
                        udpPort = port;
                    }
                    else if (!request.path.EndsWith("/tcp"))
                    {
                        writeHttpResponse(stream, "404 Not Found", "Unknown path.");
                        return;
                    }

                    string acceptKey = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(webSocketKey.Trim() + webSocketGuid)));
                    writeRaw(stream, "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + acceptKey + "\r\n\r\n");

                    client.ReceiveTimeout = 0;
                    using WebSocket ws = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(20));
                    if (udpPort == null)
                    {
                        await bridgeTcp(ws, clientIp);
                    }
                    else
                    {
                        await bridgeUdp(ws, udpPort.Value, clientIp);
                    }
                }
                catch (Exception ex)
                {
                    Helpers.debugLog("WebSocket gateway connection error: " + ex.Message);
                }
            }
        }

        // Reads the HTTP request line and headers. Reads byte by byte so nothing after the headers is consumed.
        static HttpRequest readHttpRequest(TcpClient client, NetworkStream stream)
        {
            client.ReceiveTimeout = 10000;
            var headerBytes = new List<byte>();
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1) return null;
                headerBytes.Add((byte)b);
                if (headerBytes.Count > maxHeaderBytes) return null;
                int n = headerBytes.Count;
                if (n >= 4 && headerBytes[n - 4] == '\r' && headerBytes[n - 3] == '\n' && headerBytes[n - 2] == '\r' && headerBytes[n - 1] == '\n') break;
            }

            string[] lines = Encoding.ASCII.GetString(headerBytes.ToArray()).Split("\r\n");
            string[] requestLine = lines[0].Split(' ');
            if (requestLine.Length < 2) return null;

            var request = new HttpRequest();
            string target = requestLine[1];
            int queryStart = target.IndexOf('?');
            request.path = (queryStart >= 0 ? target.Substring(0, queryStart) : target).TrimEnd('/');
            if (queryStart >= 0)
            {
                foreach (string pair in target.Substring(queryStart + 1).Split('&'))
                {
                    string[] keyValue = pair.Split('=', 2);
                    if (keyValue.Length == 2) request.query[keyValue[0]] = Uri.UnescapeDataString(keyValue[1]);
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon > 0) request.headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }
            return request;
        }

        static void writeHttpResponse(NetworkStream stream, string status, string body)
        {
            writeRaw(stream, "HTTP/1.1 " + status + "\r\nContent-Type: text/plain\r\nContent-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\nConnection: close\r\n\r\n" + body);
        }

        static void writeRaw(NetworkStream stream, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        // Matchmaking: pipes the WebSocket's bytes to and from a TCP connection to the relay
        static async Task bridgeTcp(WebSocket ws, string clientIp)
        {
            using var relay = new TcpClient();
            relay.NoDelay = true;
            await relay.ConnectAsync(IPAddress.Loopback, Global.basePort);
            int localPort = ((IPEndPoint)relay.Client.LocalEndPoint).Port;
            // Registered before any data is sent, so the relay can already resolve the real IP when it reads the request
            WebSocketTunnel.tcpClientIps[localPort] = clientIp;
            try
            {
                NetworkStream relayStream = relay.GetStream();

                Task clientToRelay = Task.Run(async () =>
                {
                    var buffer = new byte[8192];
                    while (true)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        await relayStream.WriteAsync(buffer, 0, result.Count);
                    }
                    relay.Client.Shutdown(SocketShutdown.Send);
                });

                Task relayToClient = Task.Run(async () =>
                {
                    var buffer = new byte[8192];
                    int read;
                    while ((read = await relayStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await ws.SendAsync(new ArraySegment<byte>(buffer, 0, read), WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                });

                // Once one side finishes, give the other a moment to finish too (e.g. the client acknowledging the close)
                await Task.WhenAny(clientToRelay, relayToClient);
                await Task.WhenAny(Task.WhenAll(clientToRelay, relayToClient), Task.Delay(5000));
            }
            finally
            {
                WebSocketTunnel.tcpClientIps.TryRemove(localPort, out _);
            }
        }

        // Lidgren: each WebSocket message is one datagram to or from the relay UDP port
        static async Task bridgeUdp(WebSocket ws, int relayPort, string clientIp)
        {
            using var relay = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            WebSocketTunnel.ignoreUdpConnectionReset(relay.Client);
            // Connecting also makes the socket ignore datagrams from anywhere but this relay port
            relay.Connect(IPAddress.Loopback, relayPort);
            int localPort = ((IPEndPoint)relay.Client.LocalEndPoint).Port;
            WebSocketTunnel.udpClientIps[localPort] = clientIp;

            using var cts = new CancellationTokenSource();
            long lastClientActivity = Environment.TickCount64;
            try
            {
                Task clientToRelay = Task.Run(async () =>
                {
                    var buffer = new byte[WebSocketTunnel.maxDatagramSize];
                    while (true)
                    {
                        int length = await WebSocketTunnel.receiveMessageAsync(ws, buffer, cts.Token);
                        if (length < 0) return;
                        Interlocked.Exchange(ref lastClientActivity, Environment.TickCount64);
                        await relay.SendAsync(buffer, length);
                    }
                });

                Task relayToClient = Task.Run(async () =>
                {
                    while (true)
                    {
                        var result = await relay.ReceiveAsync();
                        await ws.SendAsync(new ArraySegment<byte>(result.Buffer), WebSocketMessageType.Binary, true, cts.Token);
                    }
                });

                // Lidgren pings every few seconds while connected, so a long silence means the client is gone
                Task closeWhenIdle = Task.Run(async () =>
                {
                    while (Environment.TickCount64 - Interlocked.Read(ref lastClientActivity) < udpIdleTimeoutMs)
                    {
                        await Task.Delay(1000, cts.Token);
                    }
                });

                await Task.WhenAny(clientToRelay, relayToClient, closeWhenIdle);
            }
            finally
            {
                cts.Cancel();
                WebSocketTunnel.udpClientIps.TryRemove(localPort, out _);
            }
        }
    }
}
