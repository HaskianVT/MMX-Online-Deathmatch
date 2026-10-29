using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace MMXOnline
{
    // Hooks for platforms that can't open TCP or UDP sockets (the browser), set by their backend at startup.
    // The desktop leaves them null and uses sockets as usual.
    public static class PlatformNetwork
    {
        // Sends a matchmaking query to a WebSocket region URL and returns the raw response, or null if it fails
        public static Func<string, string, int, Task<byte[]>> matchmakingRequest;

        // Creates the transport Lidgren peers use instead of a UDP socket
        public static Func<NetDatagramTransport> createDatagramTransport;

        // Lidgren endpoint for a relay UDP port behind a WebSocket region URL
        public static Func<string, int, IPEndPoint> webSocketEndPoint;

        static readonly List<NetPeer> pumpedPeers = new List<NetPeer>();

        // Gives a peer the platform's transport, if it has one. Call before starting the peer.
        public static void configure(NetPeerConfiguration config)
        {
            if (createDatagramTransport != null) config.DatagramTransport = createDatagramTransport();
        }

        // Peers with a transport have no network thread, so the game loop pumps them every frame. Call after starting the peer.
        public static void track(NetPeer peer)
        {
            if (peer.Configuration.DatagramTransport != null) pumpedPeers.Add(peer);
        }

        public static void pumpPeers()
        {
            for (int i = pumpedPeers.Count - 1; i >= 0; i--)
            {
                var peer = pumpedPeers[i];
                peer.PumpNetwork();
                if (peer.Status == NetPeerStatus.NotRunning) pumpedPeers.RemoveAt(i);
            }
        }
    }
}
