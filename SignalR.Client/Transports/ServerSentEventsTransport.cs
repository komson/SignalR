#if NET20
using SignalR.Client.Net20.Infrastructure;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SignalR.Client.Http;
using SignalR.Client.Infrastructure;
using SignalR.Client.Transports.ServerSentEvents;

namespace SignalR.Client.Transports
{
    public class ServerSentEventsTransport : HttpBasedTransport
    {
        private int _initializedCalled;

        private const string EventSourceKey = "eventSourceStream";

        public ServerSentEventsTransport()
            : this(new DefaultHttpClient())
        {
        }

        public ServerSentEventsTransport(IHttpClient httpClient)
            : base(httpClient, "serverSentEvents")
        {
            ReconnectDelay = TimeSpan.FromSeconds(2);
            ConnectionTimeout = TimeSpan.FromSeconds(2);
        }

        /// <summary>
        /// Time allowed before failing the connect request.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; }

        /// <summary>
        /// The time to wait after a connection drops to try reconnecting.
        /// </summary>
        public TimeSpan ReconnectDelay { get; set; }

        protected override void OnStart(IConnection connection, string data, Action initializeCallback, Action<Exception> errorCallback)
        {
            OpenConnection(connection, data, initializeCallback, errorCallback);
        }

        private void Reconnect(IConnection connection, string data)
        {
            // Wait for a bit before reconnecting
#if NET20
            TaskAsyncHelper.Delay(ReconnectDelay).Then(_ =>
#else
            TaskAsyncHelper.Delay(ReconnectDelay).Then(() =>
#endif
            {
                if (connection.State == ConnectionState.Reconnecting ||
                    connection.ChangeState(ConnectionState.Connected, ConnectionState.Reconnecting))
                {
                    // Now attempt a reconnect
                    OpenConnection(connection, data, initializeCallback: null, errorCallback: null);
                }
            });
        }

        private void OpenConnection(IConnection connection, string data, Action initializeCallback, Action<Exception> errorCallback)
        {
            // If we're reconnecting add /connect to the url
            bool reconnecting = initializeCallback == null;
            var callbackInvoker = new ThreadSafeInvoker();

            var url = (reconnecting ? connection.Url : connection.Url + "connect") + GetReceiveQueryString(connection, data);

            Action<IRequest> prepareRequest = PrepareRequest(connection);

#if NET35 || NET20
            Debug.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "SSE: GET {0}", (object)url));
#else
            Debug.WriteLine("SSE: GET {0}", (object)url);
#endif

            _httpClient.PostAsync(url, request =>
            {
                prepareRequest(request);

                request.Accept = "text/event-stream";

            },new Dictionary<string, string>{{"groups", GetGroupsAsString(connection)}}).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
#if NET20
					Exception exception = ExceptionsExtensions.Unwrap(task.Exception);
#else
                    Exception exception = task.Exception.Unwrap();
#endif
                    if (!ExceptionHelper.IsRequestAborted(exception))
                    {
                        if (errorCallback != null)
                        {
                            callbackInvoker.Invoke((cb, ex) => cb(ex), errorCallback, exception);
                        }
                        else if (reconnecting)
                        {
                            // Only raise the error event if we failed to reconnect
                            connection.OnError(exception);

                            Reconnect(connection, data);
                        }
                    }
                }
                else
                {
                    IResponse response = task.Result;
                    Stream stream = response.GetResponseStream();

                    var eventSource = new EventSourceStreamReader(stream);
                    bool retry = true;

                    connection.Items[EventSourceKey] = eventSource;

                    eventSource.Opened = () =>
                    {
                        if (initializeCallback != null)
                        {
                            callbackInvoker.Invoke(initializeCallback);
                        }

                        if (reconnecting && connection.ChangeState(ConnectionState.Reconnecting, ConnectionState.Connected))
                        {
                            // Raise the reconnect event if the connection comes back up
                            connection.OnReconnected();
                        }
                    };

                    eventSource.Message = sseEvent =>
                    {
                        if (sseEvent.Type == EventType.Data)
                        {
                            if (sseEvent.Data.Equals("initialized", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }

                            bool timedOut;
                            bool disconnected;
                            ProcessResponse(connection, sseEvent.Data, out timedOut, out disconnected);

                            if (disconnected)
                            {
                                retry = false;
                            }
                        }
                    };

                    eventSource.Closed = exception =>
                    {
                        if (exception != null && !ExceptionHelper.IsRequestAborted(exception))
                        {
                            // Don't raise exceptions if the request was aborted (connection was stopped).
                            connection.OnError(exception);
                        }

                        // See http://msdn.microsoft.com/en-us/library/system.net.httpwebresponse.close.aspx
                        response.Close();

                        if (retry)
                        {
                            Reconnect(connection, data);
                        }
                        else
                        {
                            connection.Stop();
                        }
                    };

                    eventSource.Start();
                }
            });

            if (errorCallback != null)
            {
#if NET20
                TaskAsyncHelper.Delay(ConnectionTimeout).Then(_ =>
#else
                TaskAsyncHelper.Delay(ConnectionTimeout).Then(() =>
#endif
                {
                    callbackInvoker.Invoke((conn, cb) =>
                    {
                        // Stop the connection
                        Stop(conn);

                        // Connection timeout occured
                        cb(new TimeoutException());
                    },
                    connection,
                    errorCallback);
                });
            }
        }

        /// <summary>
        /// Stops even event source as well and the base connection.
        /// </summary>
        /// <param name="connection">The <see cref="IConnection"/> being aborted.</param>
        protected override void OnBeforeAbort(IConnection connection)
        {
#if NET20
            var eventSourceStream = ConnectionExtensions.GetValue<EventSourceStreamReader>(connection,EventSourceKey);
#else
            var eventSourceStream = connection.GetValue<EventSourceStreamReader>(EventSourceKey);
#endif
            if (eventSourceStream != null)
            {
                eventSourceStream.Close();
            }

            base.OnBeforeAbort(connection);
        }
    }
}
