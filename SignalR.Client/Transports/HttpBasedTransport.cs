using System;
using System.Collections.Generic;
using System.Diagnostics;
#if NET20
using Newtonsoft.Json.Serialization;
using SignalR.Client.Net20.Infrastructure;
#else
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#endif
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalR.Client.Http;

namespace SignalR.Client.Transports
{
    public abstract class HttpBasedTransport : IClientTransport
    {
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

        public Task<NegotiationResponse> Negotiate(IConnection connection)
        {
            return GetNegotiationResponse(_httpClient, connection);
        }

        internal static Task<NegotiationResponse> GetNegotiationResponse(IHttpClient httpClient, IConnection connection)
        {
            string negotiateUrl = connection.Url + "negotiate";

            return httpClient.GetAsync(negotiateUrl, connection.PrepareRequest).Then(response =>
            {
                string raw = response.ReadAsString();

                if (raw == null)
                {
                    throw new InvalidOperationException("Server negotiation failed.");
                }

                return JsonConvert.DeserializeObject<NegotiationResponse>(raw);
            });
        }

        public Task Start(IConnection connection, string data)
        {
            var tcs = new TaskCompletionSource<object>();

            OnStart(connection, data, () => tcs.TrySetResult(null), exception => tcs.TrySetException(exception));

            return tcs.Task;
        }

        protected abstract void OnStart(IConnection connection, string data, Action initializeCallback, Action<Exception> errorCallback);

        public Task<T> Send<T>(IConnection connection, string data)
        {
            string url = connection.Url + "send";
            string customQueryString = GetCustomQueryString(connection);

            url += String.Format(_sendQueryString, _transport, connection.ConnectionId, customQueryString);

            var postData = new Dictionary<string, string> {
                { "data", data }
            };

            return _httpClient.PostAsync(url, connection.PrepareRequest, postData).Then(response =>
            {
                string raw = response.ReadAsString();

                if (String.IsNullOrEmpty(raw))
                {
                    return default(T);
                }

                return JsonConvert.DeserializeObject<T>(raw);
            });
        }

        protected string GetReceiveQueryString(IConnection connection, string data)
        {
            // ?transport={0}&connectionId={1}&messageId={2}&groups={3}&connectionData={4}{5}
            var qsBuilder = new StringBuilder();
            qsBuilder.Append("?transport=" + _transport)
                     .Append("&connectionId=" + Uri.EscapeDataString(connection.ConnectionId));

            if (connection.MessageId != null)
            {
                qsBuilder.Append("&messageId=" + connection.MessageId);
            }

            if (data != null)
            {
                qsBuilder.Append("&connectionData=" + data);
            }

            string customQuery = GetCustomQueryString(connection);

            if (!String.IsNullOrEmpty(customQuery))
            {
                qsBuilder.Append("&")
                         .Append(customQuery);
            }

            return qsBuilder.ToString();
        }

		protected string GetGroupsAsString(IConnection connection)
		{
#if NET20
			if (connection.Groups != null && new List<string>(connection.Groups).Count > 0)
#else
			if (connection.Groups != null && connection.Groups.Any())
#endif
			{
				return Uri.EscapeDataString(JsonConvert.SerializeObject(connection.Groups));
			}
			return string.Empty;
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

        public void Stop(IConnection connection)
        {
#if NET20
            var httpRequest = ConnectionExtensions.GetValue<IRequest>(connection,HttpRequestKey);
#else
            var httpRequest = connection.GetValue<IRequest>(HttpRequestKey);
#endif
            if (httpRequest != null)
            {
                try
                {
                    OnBeforeAbort(connection);

                    // Abort the server side connection
                    AbortConnection(connection);

                    // Now abort the client connection
                    httpRequest.Abort();
                }
                catch (NotImplementedException)
                {
                    // If this isn't implemented then do nothing
                }
            }
        }

        private void AbortConnection(IConnection connection)
        {
            string url = connection.Url + "abort" + String.Format(_sendQueryString, _transport, connection.ConnectionId, null);

            try
            {
                // Attempt to perform a clean disconnect, but only wait 2 seconds
#if NET20
				_httpClient.PostAsync(url, connection.PrepareRequest, new Dictionary<string, string>());
#else
                _httpClient.PostAsync(url, connection.PrepareRequest).Wait(TimeSpan.FromSeconds(2));
#endif
			}
            catch (Exception ex)
            {
                // Swallow any exceptions, but log them
#if NET20
                Debug.WriteLine("Clean disconnect failed. " + ExceptionsExtensions.Unwrap(ex).Message);
#else
                Debug.WriteLine("Clean disconnect failed. " + ex.Unwrap().Message);
#endif
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
                    foreach (JToken message in messages)
                    {
                        try
                        {
                            connection.OnReceived(message);
                        }
                        catch (Exception ex)
                        {
#if NET35 || NET20
                            Debug.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "Failed to process message: {0}", ex));
#else
                            Debug.WriteLine("Failed to process message: {0}", ex);
#endif

                            connection.OnError(ex);
                        }
                    }

                    connection.MessageId = result["MessageId"].Value<string>();

                    var transportData = result["TransportData"] as JObject;

                    if (transportData != null)
                    {
                        var groups = (JArray)transportData["Groups"];
                        if (groups != null)
                        {
#if NET20
                        	var groupList = new List<string>();
                        	foreach (JToken jToken in groups)
                        	{
                        		groupList.Add(jToken.Value<string>());
                        	}
							connection.Groups = groupList;
#else
                            connection.Groups = groups.Select(token => token.Value<string>());
#endif
                        }
                    }
                }
            }
            catch (Exception ex)
            {
#if NET35 || NET20
                Debug.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "Failed to response: {0}", ex));
#else
                Debug.WriteLine("Failed to response: {0}", ex);
#endif
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
