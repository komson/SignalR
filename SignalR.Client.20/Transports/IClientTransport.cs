using System;

namespace SignalR.Client._20.Transports
{
	public interface IClientTransport
	{
		void Start(Connection connection, string data);
		EventSignal<T> Send<T>(Connection connection, string data);
		void Stop(Connection connection);
	}
}
