using NetEndPoint = System.Net.IPEndPoint;

namespace Lidgren.Network
{
	/// <summary>
	/// Sends and receives datagrams instead of a UDP socket, for platforms without one (e.g. browsers, over WebSockets).
	/// When NetPeerConfiguration.DatagramTransport is set, the peer doesn't start a network thread either;
	/// call NetPeer.PumpNetwork() regularly (e.g. every frame) to send and receive.
	/// </summary>
	public abstract class NetDatagramTransport
	{
		/// <summary>
		/// Port reported as the peer's local port
		/// </summary>
		public abstract int LocalPort { get; }

		public abstract void SendTo(byte[] data, int numBytes, NetEndPoint target);

		/// <summary>
		/// Copies the next received datagram into buffer. Returns its length, or 0 if there are none.
		/// </summary>
		public abstract int ReceiveFrom(byte[] buffer, out NetEndPoint sender);

		public abstract void Close();
	}
}
