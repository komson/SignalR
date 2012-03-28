extern alias dotnet2;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using dotnet2::Newtonsoft.Json;
using dotnet2::Newtonsoft.Json.Linq;

namespace SignalR.Client._20.Transports
{
    public abstract class HttpBasedTransport : IClientTransport
    {
        // The receive query string
        private const string _receiveQueryString = "?transport={0}&connectionId={1}&messageId={2}&connectionData={3}{4}";

        // The send query string
        private const string _sendQueryString = "?transport={0}&connectionId={1}{2}";

        // The transport name
        protected readonly string _transport;

        protected const string HttpRequestKey = "http.Request";

        public HttpBasedTransport(string transport)
        {
            _transport = transport;
        }

        public void Start(Connection connection, string data)
        {
			OnStart(connection, data, () => {}, exception => { throw exception; });
        }

		protected abstract void OnStart(Connection connection, string data, dotnet2::System.Action initializeCallback, Action<Exception> errorCallback);

        public EventSignal<T> Send<T>(Connection connection, string data)
        {
            string url = connection.Url + "send";
            string customQueryString = GetCustomQueryString(connection);

            url += String.Format(_sendQueryString, _transport, connection.ConnectionId, customQueryString);

            var postData = new Dictionary<string, string> {
                { "data", data },
				{"groups", Uri.EscapeDataString(JsonConvert.SerializeObject(connection.Groups))}
            };

        	var returnSignal = new EventSignal<T>();
        	internalSend(returnSignal, connection, url, postData, 1);
        	return returnSignal;
        }

		private void internalSend<T>(EventSignal<T> eventSignal, Connection connection, string url, IDictionary<string ,string > postData, int attemptNumber)
		{
			var postSignal = HttpHelper.PostAsync(url, connection.PrepareRequest, postData);
			postSignal.Finished += (sender, e) =>
			{
				if (e.Result.IsFaulted)
				{
					if (attemptNumber > 10)
					{
						throw e.Result.Exception;
					}

					Debug.WriteLine(string.Format("Retrying time {0}",attemptNumber));
					Thread.Sleep(TimeSpan.FromSeconds(attemptNumber*2));
					internalSend(eventSignal,connection,url,postData,attemptNumber++);

					return;
				}

				T result;
				string raw = HttpHelper.ReadAsString(e.Result.Result);

				if (String.IsNullOrEmpty(raw))
				{
					result = default(T);
				}
				else
				{
					result = JsonConvert.DeserializeObject<T>(raw);
				}
				eventSignal.OnFinish(result);
			};
		}

        protected string GetReceiveQueryString(Connection connection, string data)
        {
            return String.Format(_receiveQueryString,
                                 _transport,
                                 Uri.EscapeDataString(connection.ConnectionId),
                                 Convert.ToString(connection.MessageId),
                                 data,
                                 GetCustomQueryString(connection));
        }

        protected virtual Action<HttpWebRequest> PrepareRequest(Connection connection)
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

        public void Stop(Connection connection)
        {
            var httpRequest = ConnectionExtensions.GetValue<HttpWebRequest>(connection,HttpRequestKey);
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

        protected virtual void OnBeforeAbort(Connection connection)
        {

        }

		protected static void ProcessResponse(Connection connection, string response, out bool timedOut, out bool disconnected)
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
				disconnected = result.Value<bool>("Disconnected");

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

        private static string GetCustomQueryString(Connection connection)
        {
            return String.IsNullOrEmpty(connection.QueryString)
                            ? ""
                            : "&" + connection.QueryString;
        }
    }
}
