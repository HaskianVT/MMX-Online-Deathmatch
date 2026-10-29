using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MMXOnline
{
    // Lets the game reach a relay server through a WebSocket URL (e.g. "wss://mmx.example.com") instead of an IP or domain.
    // This allows hosting the relay behind a Cloudflare Tunnel, which only needs outgoing connections and so works behind CG-NAT.
    // Each connection to the relay is carried in its own WebSocket, handled on the relay side by RelayServer.WebSocketGateway:
    //   /tcp         -> the bytes of a matchmaking TCP connection (relay port Global.basePort)
    //   /udp?port=N  -> one WebSocket message per UDP datagram, to and from relay UDP port N (Lidgren)
    public static class WebSocketTunnel
    {
        // Local port the relay's WebSocket gateway listens on. cloudflared should forward the public hostname to http://127.0.0.1:14240
        public const int gatewayPort = Global.basePort - 2;
        // Relay UDP ports the gateway may forward to: the matchmaking port and the match ports after it
        public const int maxUdpPortOffset = 100;
        public const int maxDatagramSize = 65536;

        public static bool isWebSocketUrl(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return false;
            return Uri.TryCreate(address.Trim(), UriKind.Absolute, out Uri uri) && (uri.Scheme == "ws" || uri.Scheme == "wss") && !string.IsNullOrEmpty(uri.Host);
        }

        public static Uri buildUri(string baseUrl, string path, string query = "")
        {
            var builder = new UriBuilder(baseUrl.Trim());
            builder.Path = builder.Path.TrimEnd('/') + path;
            builder.Query = query;
            return builder.Uri;
        }

        // Matchmaking: returns a stream that behaves like the TCP connection to the relay
        public static Stream openTcpStream(string baseUrl, int timeoutMs)
        {
            var ws = new ClientWebSocket();
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                ws.ConnectAsync(buildUri(baseUrl, "/tcp"), cts.Token).GetAwaiter().GetResult();
            }
            catch
            {
                ws.Dispose();
                throw;
            }
            return new WebSocketTunnelStream(ws) { ReadTimeout = timeoutMs, WriteTimeout = timeoutMs };
        }

        // Lidgren: opens a local UDP port that forwards to relay UDP port relayPort. Connect the NetClient to 127.0.0.1 on the returned port.
        // The tunnel closes by itself once Lidgren stops sending (it pings every few seconds while connected).
        public static int openUdpTunnel(string baseUrl, int relayPort)
        {
            var tunnel = new UdpTunnelClient(buildUri(baseUrl, "/udp", "port=" + relayPort));
            return tunnel.localPort;
        }

        // Relay side: the gateway connects to the relay from localhost, so it records here which real client IP is behind each of its local ports
        public static readonly ConcurrentDictionary<int, string> tcpClientIps = new ConcurrentDictionary<int, string>();
        public static readonly ConcurrentDictionary<int, string> udpClientIps = new ConcurrentDictionary<int, string>();

        // IP address of a client connected to the relay, resolving connections that came through the WebSocket gateway
        public static string getClientIp(EndPoint endPoint, bool isUdp)
        {
            if (endPoint is not IPEndPoint ipEndPoint) return null;
            var clientIps = isUdp ? udpClientIps : tcpClientIps;
            if (IPAddress.IsLoopback(ipEndPoint.Address) && clientIps.TryGetValue(ipEndPoint.Port, out string clientIp))
            {
                return clientIp;
            }
            return ipEndPoint.Address.ToString();
        }

        // On Windows, an ICMP "port unreachable" caused by an earlier send makes the next UDP receive fail with ConnectionReset. Ignore those.
        public static void ignoreUdpConnectionReset(Socket socket)
        {
            const int SIO_UDP_CONNRESET = -1744830452;
            try
            {
                socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch
            {
                // Not supported on this platform
            }
        }

        // Reads one complete WebSocket message into buffer. Returns its length, or -1 if the WebSocket was closed.
        public static async Task<int> receiveMessageAsync(WebSocket ws, byte[] buffer, CancellationToken cancellationToken)
        {
            int count = 0;
            while (true)
            {
                if (count == buffer.Length) throw new InvalidDataException("WebSocket message too large.");
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return -1;
                count += result.Count;
                if (result.EndOfMessage) return count;
            }
        }
    }

    // Client side of a UDP tunnel: Lidgren sends to 127.0.0.1:localPort and the datagrams travel over the WebSocket to the relay
    class UdpTunnelClient
    {
        const int connectTimeoutMs = 10000;
        const int idleTimeoutMs = 30000;

        public readonly int localPort;
        readonly UdpClient local;
        readonly ClientWebSocket ws = new ClientWebSocket();
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        IPEndPoint lidgrenEndPoint;
        long lastLidgrenActivity;

        public UdpTunnelClient(Uri uri)
        {
            local = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            WebSocketTunnel.ignoreUdpConnectionReset(local.Client);
            localPort = ((IPEndPoint)local.Client.LocalEndPoint).Port;
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            Task.Run(() => run(uri));
        }

        async Task run(Uri uri)
        {
            try
            {
                using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
                {
                    connectCts.CancelAfter(connectTimeoutMs);
                    await ws.ConnectAsync(uri, connectCts.Token).ConfigureAwait(false);
                }
                // Datagrams Lidgren sent while the WebSocket was connecting are still waiting in the local socket's buffer
                Interlocked.Exchange(ref lastLidgrenActivity, Environment.TickCount64);
                await Task.WhenAny(lidgrenToRelay(), relayToLidgren(), closeWhenIdle()).ConfigureAwait(false);
            }
            catch
            {
                // Connection failed or dropped; Lidgren will time out on its own
            }
            finally
            {
                cts.Cancel();
                local.Dispose();
                ws.Abort();
                ws.Dispose();
            }
        }

        async Task lidgrenToRelay()
        {
            while (true)
            {
                var result = await local.ReceiveAsync().ConfigureAwait(false);
                lidgrenEndPoint = result.RemoteEndPoint;
                Interlocked.Exchange(ref lastLidgrenActivity, Environment.TickCount64);
                await ws.SendAsync(new ArraySegment<byte>(result.Buffer), WebSocketMessageType.Binary, true, cts.Token).ConfigureAwait(false);
            }
        }

        async Task relayToLidgren()
        {
            var buffer = new byte[WebSocketTunnel.maxDatagramSize];
            while (true)
            {
                int length = await WebSocketTunnel.receiveMessageAsync(ws, buffer, cts.Token).ConfigureAwait(false);
                if (length < 0) return;
                var target = lidgrenEndPoint;
                if (target != null)
                {
                    await local.SendAsync(buffer, length, target).ConfigureAwait(false);
                }
            }
        }

        async Task closeWhenIdle()
        {
            while (Environment.TickCount64 - Interlocked.Read(ref lastLidgrenActivity) < idleTimeoutMs)
            {
                await Task.Delay(1000, cts.Token).ConfigureAwait(false);
            }
        }
    }

    // Synchronous Stream over a WebSocket, so the matchmaking code can use it like a NetworkStream
    public class WebSocketTunnelStream : Stream
    {
        readonly WebSocket ws;
        readonly byte[] receiveBuffer = new byte[8192];
        int receiveOffset;
        int receiveCount;
        bool remoteClosed;

        public WebSocketTunnelStream(WebSocket ws)
        {
            this.ws = ws;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override bool CanTimeout => true;
        public override int ReadTimeout { get; set; } = Timeout.Infinite;
        public override int WriteTimeout { get; set; } = Timeout.Infinite;

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (receiveCount == 0)
            {
                if (remoteClosed) return 0;
                using var cts = new CancellationTokenSource(ReadTimeout);
                var result = ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cts.Token).GetAwaiter().GetResult();
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    remoteClosed = true;
                    return 0;
                }
                receiveOffset = 0;
                receiveCount = result.Count;
            }

            int copied = Math.Min(count, receiveCount);
            Array.Copy(receiveBuffer, receiveOffset, buffer, offset, copied);
            receiveOffset += copied;
            receiveCount -= copied;
            return copied;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            using var cts = new CancellationTokenSource(WriteTimeout);
            ws.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, true, cts.Token).GetAwaiter().GetResult();
        }

        public override void Flush() { }
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                    {
                        using var cts = new CancellationTokenSource(1000);
                        ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token).GetAwaiter().GetResult();
                    }
                }
                catch
                {
                    // Closing is best effort
                }
                ws.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
