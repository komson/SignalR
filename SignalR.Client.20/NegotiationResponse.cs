using System.Diagnostics;

namespace SignalR.Client._20
{
	[DebuggerDisplay("{ConnectionId} {Url} -> {ProtocolVersion}")]
	public class NegotiationResponse
	{
		public string ConnectionId { get; set; }
		public string Url { get; set; }
		public string ProtocolVersion { get; set; }
	}
}
