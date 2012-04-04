﻿extern alias dotnet2;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using dotnet2::Newtonsoft.Json;
using dotnet2::Newtonsoft.Json.Linq;
using SignalR.Client._20.Http;

namespace SignalR.Client._20.Transports
{
    public abstract class HttpBasedTransport : IClientTransport
	{
		// The receive query string
		private const string _receiveQueryStringWithGroups = "?transport={0}&connectionId={1}&messageId={2}&groups={3}&connectionData={4}{5}";
		private const string _receiveQueryString = "?transport={0}&connectionId={1}&messageId={2}&connectionData={3}{4}";

		// The send query string
		private const string _sendQueryString = "?transport={0}&connectionId={1}{2}";

        // The transport name
        protected readonly string _transport;

        protected const string HttpRequestKey = "http.Request";

		protected readonly IHttpClient _httpClient;

		public HttpBasedTransport(IHttpClient httpClient, string transport)
		{
			_httpClient = httpClient;
			_transport = transport;
		}

		public EventSignal<NegotiationResponse> Negotiate(IConnection connection)
		{
			return GetNegotiationResponse(_httpClient, connection);
		}

		internal static EventSignal<NegotiationResponse> GetNegotiationResponse(IHttpClient httpClient, IConnection connection)
		{
			string negotiateUrl = connection.Url + "negotiate";

			var negotiateSignal = new EventSignal<NegotiationResponse>();
			var signal = httpClient.PostAsync(negotiateUrl, connection.PrepareRequest, new Dictionary<string, string>());
			signal.Finished += (sender,e) =>
			{
				string raw = e.Result.ReadAsString();

				if (raw == null)
				{
					throw new InvalidOperationException("Server negotiation failed.");
				}

				negotiateSignal.OnFinish(JsonConvert.DeserializeObject<NegotiationResponse>(raw));
			};
			return negotiateSignal;
		}

        public void Start(IConnection connection, string data)
        {
			OnStart(connection, data, () => {}, exception => { throw exception; });
        }

		protected abstract void OnStart(IConnection connection, string data, dotnet2::System.Action initializeCallback, Action<Exception> errorCallback);

        public EventSignal<T> Send<T>(IConnection connection, string data)
        {
            string url = connection.Url + "send";
            string customQueryString = GetCustomQueryString(connection);

            url += String.Format(_sendQueryString, _transport, connection.ConnectionId, customQueryString);

            var postData = new Dictionary<string, string> {
                { "data", data },
            };

        	var returnSignal = new EventSignal<T>();
			var postSignal = _httpClient.PostAsync(url, connection.PrepareRequest, postData);
        	postSignal.Finished += (sender, e) =>
        	                       	{
        	                       		string raw = e.Result.ReadAsString();

        	                       		if (String.IsNullOrEmpty(raw))
        	                       		{
        	                       			returnSignal.OnFinish(default(T));
        	                       			return;
        	                       		}

        	                       		returnSignal.OnFinish(JsonConvert.DeserializeObject<T>(raw));
        	                       	};
        	return returnSignal;
        }

        protected string GetReceiveQueryStringWithGroups(IConnection connection, string data)
        {
            return String.Format(_receiveQueryStringWithGroups,
                                 _transport,
                                 Uri.EscapeDataString(connection.ConnectionId),
								 Convert.ToString(connection.MessageId),
								 GetSerializedGroups(connection),
                                 data,
                                 GetCustomQueryString(connection));
        }

		protected string GetSerializedGroups(IConnection connection)
		{
			return Uri.EscapeDataString(JsonConvert.SerializeObject(connection.Groups));
		}

		protected string GetReceiveQueryString(IConnection connection, string data)
		{
			return String.Format(_receiveQueryString,
								 _transport,
								 Uri.EscapeDataString(connection.ConnectionId),
								 Convert.ToString(connection.MessageId),
								 data,
								 GetCustomQueryString(connection));
		}

        protected virtual Action<IRequest> PrepareRequest(IConnection connection)
        {
            return request =>
            {
                // Setup the user agent along with any other defaults
                connection.PrepareRequest(request);

                connection.Items[HttpRequestKey] = request;
            };
        }

        protected static bool IsRequestAborted(Exception exception)
        {
            var webException = exception as WebException;
            return (webException != null && webException.Status == WebExceptionStatus.RequestCanceled);
        }

        public void Stop(IConnection connection)
        {
            var httpRequest = ConnectionExtensions.GetValue<IRequest>(connection,HttpRequestKey);
            if (httpRequest != null)
            {
                try
                {
                    OnBeforeAbort(connection);
                    httpRequest.Abort();
                }
                catch (NotImplementedException)
                {
                    // If this isn't implemented then do nothing
                }
            }
        }

        protected virtual void OnBeforeAbort(IConnection connection)
        {

        }

		protected static void ProcessResponse(IConnection connection, string response, out bool timedOut, out bool disconnected)
		{
			timedOut = false;
			disconnected = false;

			if (String.IsNullOrEmpty(response))
			{
				return;
			}

            if (connection.MessageId == null)
            {
                connection.MessageId = 0;
            }

            try
            {
                var result = JValue.Parse(response);

                if (!result.HasValues)
                {
                    return;
                }

				timedOut = result.Value<bool>("TimedOut");
				disconnected = result.Value<bool>("Disconnect");

				if (disconnected)
				{
					return;
				}

                var messages = result["Messages"] as JArray;
                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        try
                        {
                            connection.OnReceived(message.ToString());
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(string.Format("Failed to process message: {0}", ex));
                            connection.OnError(ex);
                        }
                    }

                    connection.MessageId = Extensions.Value<long>(result["MessageId"]);

                    var transportData = result["TransportData"] as JObject;

                    if (transportData != null)
                    {
                        var groups = (JArray)transportData["Groups"];
                        if (groups != null)
                        {
                        	var groupList = new List<string>();
                        	foreach (var groupFromTransport in groups)
                        	{
                        		groupList.Add(Extensions.Value<string>(groupFromTransport));
                        	}
                            connection.Groups = groupList;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Failed to response: {0}", ex));
                connection.OnError(ex);
            }
        }

        private static string GetCustomQueryString(IConnection connection)
        {
            return String.IsNullOrEmpty(connection.QueryString)
                            ? ""
                            : "&" + connection.QueryString;
        }
    }
}
