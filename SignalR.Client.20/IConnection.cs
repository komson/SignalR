extern alias dotnet2;

using System;
using System.Collections.Generic;
using System.Net;
using SignalR.Client._20.Http;
using SignalR.Client._20.Infrastructure;
using SignalR.Client._20.Transports;

namespace SignalR.Client._20
{
    public interface IConnection
    {
        bool IsActive { get; }
        long? MessageId { get; set; }
		dotnet2::System.Func<string> Sending { get; set; }
		IEnumerable<string> Groups { get; set; }
		IDictionary<string, object> Items { get; }
		string ConnectionId { get; }
		string Url { get; }
		string QueryString { get; }

        ICredentials Credentials { get; set; }
		CookieContainer CookieContainer { get; set; }

		event dotnet2::System.Action Closed;
        event Action<Exception> Error;
        event Action<string> Received;

        void Stop();
		EventSignal<object> Send(string data);
        EventSignal<T> Send<T>(string data);

		void OnReceived(dotnet2::Newtonsoft.Json.Linq.JToken data);
		void OnError(Exception ex);
		void OnReconnected();
		void PrepareRequest(IRequest request);
    }
}
