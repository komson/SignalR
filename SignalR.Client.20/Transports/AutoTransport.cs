using System;

namespace SignalR.Client._20.Transports
{
    public class AutoTransport : IClientTransport
    {
        // Transport that's in use
        private IClientTransport _transport;

        // List of transports in fallback order
		private readonly IClientTransport[] _transports = new IClientTransport[] { new ServerSentEventsTransport(), new LongPollingTransport() };

        public void Start(Connection connection, string data)
        {
            // Resolve the transport
            ResolveTransport(connection, data, 0);
        }

		private void ResolveTransport(Connection connection, string data, int index)
		{
			// Pick the current transport
			IClientTransport transport = _transports[index];

			try
			{
				transport.Start(connection, data);
				_transport = transport;
			}
			catch (Exception)
			{
				var next = index + 1;
				if (next < _transports.Length)
				{
					// Try the next transport
					ResolveTransport(connection, data, next);
				}
				else
				{
					// If there's nothing else to try then just fail
					throw new NotSupportedException("The transports available were not supported on this client.");
				}
			}
		}

    	public EventSignal<T> Send<T>(Connection connection, string data)
        {
            return _transport.Send<T>(connection, data);
        }

        public void Stop(Connection connection)
        {
            _transport.Stop(connection);
        }
    }
}
