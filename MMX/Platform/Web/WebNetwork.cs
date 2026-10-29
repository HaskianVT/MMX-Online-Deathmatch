using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

namespace MMXOnline
{
    // Browser networking: browsers can't open TCP or UDP sockets, so both matchmaking and Lidgren go through
    // WebSockets to the relay's gateway (see RelayServer/WebSocketGateway.cs), using net.js. Only WebSocket regions ("wss://...") work.
    internal static partial class WebNetwork
    {
        public static void install()
        {
            PlatformNetwork.matchmakingRequest = matchmakingRequest;
            PlatformNetwork.createDatagramTransport = () => new WebSocketDatagramTransport();
            PlatformNetwork.webSocketEndPoint = endPointFor;
        }

        static async Task<byte[]> matchmakingRequest(string ip, string query, int timeoutMs)
        {
            if (!WebSocketTunnel.isWebSocketUrl(ip)) return null;
            string response = await matchmakingRequestJs(WebSocketTunnel.buildUri(ip, "/tcp").ToString(), query, timeoutMs);
            return response == null ? null : Convert.FromBase64String(response);
        }

        // Each relay UDP port behind a WebSocket region gets a made-up endpoint for Lidgren to address,
        // which WebSocketDatagramTransport maps back to the gateway URL
        static readonly Dictionary<string, int> regionIndexes = new Dictionary<string, int>();
        internal static readonly Dictionary<IPEndPoint, string> endPointUrls = new Dictionary<IPEndPoint, string>();

        static IPEndPoint endPointFor(string baseUrl, int relayPort)
        {
            if (!regionIndexes.TryGetValue(baseUrl, out int index))
            {
                index = regionIndexes.Count + 1;
                regionIndexes[baseUrl] = index;
            }
            var endPoint = new IPEndPoint(new IPAddress(new byte[] { 127, 1, (byte)(index >> 8), (byte)index }), relayPort);
            endPointUrls[endPoint] = WebSocketTunnel.buildUri(baseUrl, "/udp", "port=" + relayPort).ToString();
            return endPoint;
        }

        [JSImport("net.matchmakingRequest", "platform")]
        [return: JSMarshalAs<JSType.Promise<JSType.String>>]
        private static partial Task<string> matchmakingRequestJs(string url, string query, int timeoutMs);

        [JSImport("net.udpOpen", "platform")]
        internal static partial int udpOpen(string url);

        [JSImport("net.udpSend", "platform")]
        internal static partial void udpSend(int socket, [JSMarshalAs<JSType.MemoryView>] Span<byte> datagram);

        [JSImport("net.udpReceive", "platform")]
        internal static partial int udpReceive(int socket, [JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

        [JSImport("net.udpClose", "platform")]
        internal static partial void udpClose(int socket);
    }

    // Lidgren transport that sends each datagram as a WebSocket message, with one WebSocket per remote endpoint
    internal class WebSocketDatagramTransport : NetDatagramTransport
    {
        static int nextLocalPort = 50000;

        readonly int localPort = nextLocalPort++;
        readonly Dictionary<IPEndPoint, int> sockets = new Dictionary<IPEndPoint, int>();

        public override int LocalPort => localPort;

        public override void SendTo(byte[] data, int numBytes, IPEndPoint target)
        {
            if (!sockets.TryGetValue(target, out int socket))
            {
                if (!WebNetwork.endPointUrls.TryGetValue(target, out string url)) return;
                socket = WebNetwork.udpOpen(url);
                // Copy the endpoint, since Lidgren may reuse the instance it passed in
                sockets[new IPEndPoint(target.Address, target.Port)] = socket;
            }
            WebNetwork.udpSend(socket, data.AsSpan(0, numBytes));
        }

        public override int ReceiveFrom(byte[] buffer, out IPEndPoint sender)
        {
            foreach (var (endPoint, socket) in sockets)
            {
                int length = WebNetwork.udpReceive(socket, buffer);
                if (length > 0)
                {
                    sender = new IPEndPoint(endPoint.Address, endPoint.Port);
                    return length;
                }
            }
            sender = null;
            return 0;
        }

        public override void Close()
        {
            foreach (var socket in sockets.Values) WebNetwork.udpClose(socket);
            sockets.Clear();
        }
    }
}
