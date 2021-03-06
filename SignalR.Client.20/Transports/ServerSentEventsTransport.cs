﻿extern alias dotnet2;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SignalR.Client._20.Http;
using SignalR.Client._20.Infrastructure;

namespace SignalR.Client._20.Transports
{
    public class ServerSentEventsTransport : HttpBasedTransport
    {
        private const string ReaderKey = "sse.reader";
		private int _initializedCalled;

        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(2);

		public ServerSentEventsTransport()
			: this(new DefaultHttpClient())
		{
		}

		public ServerSentEventsTransport(IHttpClient httpClient)
			: base(httpClient, "serverSentEvents")
		{
			ConnectionTimeout = TimeSpan.FromSeconds(2);
		}

        /// <summary>
        /// Time allowed before failing the connect request
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; }

		protected override void OnStart(IConnection connection, string data, dotnet2::System.Action initializeCallback, Action<Exception> errorCallback)
        {
            OpenConnection(connection, data, initializeCallback, errorCallback);
        }

		private void Reconnect(IConnection connection, string data)
		{
			if (!connection.IsActive)
			{
				return;
			}

			// Wait for a bit before reconnecting
			Thread.Sleep(ReconnectDelay);

			// Now attempt a reconnect
			OpenConnection(connection, data, initializeCallback: null, errorCallback: null);
		}

		private void OpenConnection(IConnection connection, string data, dotnet2::System.Action initializeCallback, Action<Exception> errorCallback)
        {
            // If we're reconnecting add /connect to the url
            bool reconnecting = initializeCallback == null;

			var url = (reconnecting ? connection.Url : connection.Url + "connect");

			Action<IRequest> prepareRequest = PrepareRequest(connection);

			EventSignal<IResponse> signal;
			
			if (shouldUsePost(connection))
			{
				url += GetReceiveQueryString(connection, data);

				Debug.WriteLine(string.Format("SSE: POST {0}", url));

				signal = _httpClient.PostAsync(url, request =>
				                                    	{
				                                    		prepareRequest(request);
				                                    		request.Accept = "text/event-stream";
				                                    	}, new Dictionary<string, string> {{"groups", GetSerializedGroups(connection)}});
			}
			else
			{
				url += GetReceiveQueryStringWithGroups(connection, data);

				Debug.WriteLine(string.Format("SSE: GET {0}", url));

				signal = _httpClient.GetAsync(url, request =>
				{
					prepareRequest(request);

					request.Accept = "text/event-stream";
				});
			}

			signal.Finished += (sender,e)=> {
                if (e.Result.IsFaulted)
                {
					var exception = e.Result.Exception.GetBaseException();
					if (!IsRequestAborted(exception))
					{
						if (errorCallback != null &&
							Interlocked.Exchange(ref _initializedCalled, 1) == 0)
						{
							errorCallback(exception);
						}
						else if (reconnecting)
						{
							// Only raise the error event if we failed to reconnect
							connection.OnError(exception);
						}
					}

					if (reconnecting)
					{
						// Retry
						Reconnect(connection, data);
						return;
					}
				}
                else
                {
                    // Get the reseponse stream and read it for messages
                	var response = e.Result;
					var stream = response.GetResponseStream();
					var reader = new AsyncStreamReader(stream,
													   connection,
													   () =>
													   {
														   if (Interlocked.CompareExchange(ref _initializedCalled, 1, 0) == 0)
														   {
															   initializeCallback();
														   }
													   },
													   () =>
													   {
														   response.Close();

														   Reconnect(connection, data);
													   });

					if (reconnecting)
					{
						// Raise the reconnect event if the connection comes back up
						connection.OnReconnected();
					}

					reader.StartReading();

					// Set the reader for this connection
					connection.Items[ReaderKey] = reader;
                }
            };

			if (initializeCallback != null)
			{
				Thread.Sleep(ConnectionTimeout);
				if (Interlocked.CompareExchange(ref _initializedCalled, 1, 0) == 0)
				{
					// Stop the connection
					Stop(connection);

					// Connection timeout occured
					errorCallback(new TimeoutException());
				}
			}
        }

    	private static bool shouldUsePost(IConnection connection)
    	{
    		return new List<string>(connection.Groups).Count>20;
    	}

    	protected override void OnBeforeAbort(IConnection connection)
        {
            // Get the reader from the connection and stop it
            var reader = ConnectionExtensions.GetValue<AsyncStreamReader>(connection, ReaderKey);
            if (reader != null)
            {
                // Stop reading data from the stream
                reader.StopReading(false);

                // Remove the reader
                connection.Items.Remove(ReaderKey);
            }
        }

        private class AsyncStreamReader
        {
            private readonly Stream _stream;
            private readonly ChunkBuffer _buffer;
			private readonly dotnet2::System.Action _initializeCallback;
			private readonly dotnet2::System.Action _closeCallback;
            private readonly IConnection _connection;
            private int _processingQueue;
            private int _reading;
            private bool _processingBuffer;

			public AsyncStreamReader(Stream stream, IConnection connection, dotnet2::System.Action initializeCallback, dotnet2::System.Action closeCallback)
            {
                _initializeCallback = initializeCallback;
                _closeCallback = closeCallback;
                _stream = stream;
                _connection = connection;
                _buffer = new ChunkBuffer();
            }

			public bool Reading
			{
				get
				{
					return _reading == 1;
				}
			}

            public void StartReading()
            {
				if (Interlocked.Exchange(ref _reading, 1) == 0)
				{
					ReadLoop();
				}
            }

            public void StopReading(bool raiseCloseCallback = true)
            {
				if (Interlocked.Exchange(ref _reading, 0) == 1)
				{
					if (raiseCloseCallback)
					{
						_closeCallback();
					}
				}
            }

            private void ReadLoop()
            {
                if (!Reading)
                {
                    return;
                }

                var buffer = new byte[1024];

				var signal = new EventSignal<CallbackDetail<int>>();
				signal.Finished += (sender, e) =>
				{
					if (e.Result.IsFaulted)
					{
						Exception exception = e.Result.Exception.GetBaseException();

						if (!IsRequestAborted(exception))
						{
							if (!(exception is IOException))
							{
								_connection.OnError(exception);
							}

							StopReading();
						}
						return;
					}

					int read = e.Result.Result;

					if (read > 0)
					{
						// Put chunks in the buffer
						_buffer.Add(buffer, read);
					}

					if (read == 0)
					{
						// Stop any reading we're doing
						StopReading();

						return;
					}

					// Keep reading the next set of data
					ReadLoop();

					if (read <= buffer.Length)
					{
						// If we read less than we wanted or if we filled the buffer, process it
						ProcessBuffer();
					}
				};
                StreamExtensions.ReadAsync(signal,_stream,buffer);
            }

            private void ProcessBuffer()
            {
                if (!Reading)
                {
                    return;
                }

                if (_processingBuffer)
                {
                    // Increment the number of times we should process messages
                    _processingQueue++;
                    return;
                }

                _processingBuffer = true;

                int total = Math.Max(1, _processingQueue);

                for (int i = 0; i < total; i++)
                {
                    if (!Reading)
                    {
                        return;
                    }

                    ProcessChunks();
                }

                if (_processingQueue > 0)
                {
                    _processingQueue -= total;
                }

                _processingBuffer = false;
            }

            private void ProcessChunks()
            {
                while (Reading && _buffer.HasChunks)
                {
                    string line = _buffer.ReadLine();

                    // No new lines in the buffer so stop processing
                    if (line == null)
                    {
                        break;
                    }

                    if (!Reading)
                    {
                        return;
                    }

                    // Try parsing the sseEvent
                    SseEvent sseEvent;
                    if (!TryParseEvent(line, out sseEvent))
                    {
                        continue;
                    }

                    if (!Reading)
                    {
                        return;
                    }

					Debug.WriteLine("SSE READ: " + sseEvent);

                    switch (sseEvent.Type)
                    {
                        case EventType.Id:
                            long id;
                            if (Int64.TryParse(sseEvent.Data, out id))
                            {
                                _connection.MessageId = id;
                            }
                            break;
                        case EventType.Data:
                            if (sseEvent.Data.Equals("initialized", StringComparison.OrdinalIgnoreCase))
                            {
                                if (_initializeCallback != null)
                                {
                                    // Mark the connection as started
                                    _initializeCallback();
                                }
                            }
                            else
                            {
                                if (Reading)
                                {
									// We don't care about timedout messages here since it will just reconnect
									// as part of being a long running request
									bool timedOutReceived;
									bool disconnectReceived;

									ProcessResponse(_connection, sseEvent.Data, out timedOutReceived, out disconnectReceived);

									if (disconnectReceived)
									{
										_connection.Stop();
									}

									if (timedOutReceived)
									{
										return;
									}
                                }
                            }
                            break;
                    }
                }
            }

            private bool TryParseEvent(string line, out SseEvent sseEvent)
            {
                sseEvent = null;

                if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    string data = line.Substring("data:".Length).Trim();
                    sseEvent = new SseEvent(EventType.Data, data);
                    return true;
                }
                else if (line.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                {
                    string data = line.Substring("id:".Length).Trim();
                    sseEvent = new SseEvent(EventType.Id, data);
                    return true;
                }

                return false;
            }

            private class SseEvent
            {
                public SseEvent(EventType type, string data)
                {
                    Type = type;
                    Data = data;
                }

                public EventType Type { get; private set; }
                public string Data { get; private set; }
            }

            private enum EventType
            {
                Id,
                Data
            }
        }
    }
}
