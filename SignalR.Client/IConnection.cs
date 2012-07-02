﻿#if NET20
using SignalR.Client.Net20.Infrastructure;
#else
using System.Threading.Tasks;
#endif
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using SignalR.Client.Http;

namespace SignalR.Client
{
    public interface IConnection
    {
        string MessageId { get; set; }
        IEnumerable<string> Groups { get; set; }
        IDictionary<string, object> Items { get; }
        string ConnectionId { get; }
        string Url { get; }
        string QueryString { get; }
        ConnectionState State { get; }

        bool ChangeState(ConnectionState oldState, ConnectionState newState);

        ICredentials Credentials { get; set; }
        CookieContainer CookieContainer { get; set; }

        void Stop();
        Task Send(string data);
        Task<T> Send<T>(string data);

        void OnReceived(JToken data);
        void OnError(Exception ex);
        void OnReconnected();
        void PrepareRequest(IRequest request);
    }
}
