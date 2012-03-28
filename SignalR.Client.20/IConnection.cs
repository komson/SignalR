extern alias dotnet2;

using System;
using System.Collections.Generic;
using System.Net;
using SignalR.Client._20.Transports;

namespace SignalR.Client._20
{
    public interface IConnection
    {
        bool IsActive { get; }
        long? MessageId { get; set; }
		dotnet2::System.Func<string> Sending { get; set; }
        IEnumerable<string> Groups { get; }
		IDictionary<string, object> Items { get; }
		string ConnectionId { get; }
        string Url { get; }

        ICredentials Credentials { get; set; }

		event dotnet2::System.Action Closed;
        event Action<Exception> Error;
        event Action<string> Received;

        void Stop();
		EventSignal<object> Send(string data);
        EventSignal<T> Send<T>(string data);
    }
}
