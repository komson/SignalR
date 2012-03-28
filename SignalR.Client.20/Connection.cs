﻿extern alias dotnet2;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Threading;
using dotnet2::Newtonsoft.Json;
using SignalR.Client._20.Transports;

namespace SignalR.Client._20
{
	public class Connection : IConnection
	{
		private static Version _assemblyVersion;

		private IClientTransport _transport;
		private bool _initialized;

		private readonly SynchronizationContext _syncContext;

		public event Action<string> Received;
		public event Action<Exception> Error;
		public event dotnet2::System.Action Closed;
		public event dotnet2::System.Action Reconnected;

		public Connection(string url)
			: this(url, (string)null)
		{

		}

		public Connection(string url, IDictionary<string, string> queryString)
			: this(url, CreateQueryString(queryString))
		{

		}

		public Connection(string url, string queryString)
		{
			if (url.Contains("?"))
			{
				throw new ArgumentException("Url cannot contain QueryString directly. Pass QueryString values in using available overload.", "url");
			}

			if (!url.EndsWith("/"))
			{
				url += "/";
			}

			Url = url;
			QueryString = queryString;
			Groups = new List<string>();
			Items = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			_syncContext = null;// SynchronizationContext.Current;
		}

		public CookieContainer CookieContainer { get; set; }

		public ICredentials Credentials { get; set; }

		public IEnumerable<string> Groups { get; internal set; }

		public dotnet2::System.Func<string> Sending { get; set; }

		public string Url { get; private set; }

		public bool IsActive { get; private set; }

		public long? MessageId { get; set; }

		public string ConnectionId { get; set; }

		public IDictionary<string, object> Items { get; private set; }

		public string QueryString { get; private set; }

		public void Start()
		{
			// Pick the best transport supported by the client
			Start(new AutoTransport());
		}

		public virtual void Start(IClientTransport transport)
		{
			if (IsActive)
			{
				return;
			}

			IsActive = true;

			_transport = transport;

			Negotiate();
		}

		private void Negotiate()
		{
			string negotiateUrl = Url + "negotiate";
			
			ManualResetEvent manualResetEvent = new ManualResetEvent(false);
			
			var signal = HttpHelper.PostAsync(negotiateUrl);
			signal.Finished += (sender, e) =>
			                   	{
			                   		string raw = HttpHelper.ReadAsString(e.Result.Result);
			                   		if (raw == null)
			                   		{
			                   			throw new InvalidOperationException("Server negotiation failed.");
			                   		}

			                   		var negotiationResponse = JsonConvert.DeserializeObject<NegotiationResponse>(raw);

			                   		VerifyProtocolVersion(negotiationResponse.ProtocolVersion);

			                   		ConnectionId = negotiationResponse.ConnectionId;

			                   		
									if (Sending!=null)
									{
										/* Ignore for now!
										 * if (_syncContext!=null)
										{
											string data;
											_syncContext.Post(_ =>
											                  	{
											                  		data = Sending();
																	StartTransport(data);
																	manualResetEvent.Set();
											                  	}, null);
										}
										else*/
										{
											var data = Sending();
											StartTransport(data);
											manualResetEvent.Set();
										}
									}
									else
									{
										StartTransport(null);
										manualResetEvent.Set();
									}
			                   	};
			manualResetEvent.WaitOne();

			_initialized = true;
		}

		private void StartTransport(string data)
		{
			_transport.Start(this, data);
		}

		private void VerifyProtocolVersion(string versionString)
		{
			Version version;
			if (String.IsNullOrEmpty(versionString) ||
				!TryParseVersion(versionString, out version) ||
				!(version.Major == 1 && version.Minor == 0))
			{
				throw new InvalidOperationException("Incompatible protocol version.");
			}
		}

		public virtual void Stop()
		{
			// Do nothing if the connection was never started
			if (!_initialized)
			{
				return;
			}

			try
			{
				_transport.Stop(this);

				if (Closed != null)
				{
					if (_syncContext != null)
					{
						_syncContext.Post(_ => Closed(), null);
					}
					else
					{
						Closed();
					}
				}
			}
			finally
			{
				IsActive = false;
				_initialized = false;
			}
		}

		public EventSignal<object> Send(string data)
		{
			return Send<object>(data);
		}

		public EventSignal<T> Send<T>(string data)
		{
			if (!_initialized)
			{
				throw new InvalidOperationException("Start must be called before data can be sent");
			}

			return _transport.Send<T>(this, data);
		}

		internal void OnReceived(string message)
		{
			if (Received != null)
			{
				if (_syncContext != null)
				{
					_syncContext.Post(msg => Received((string)msg), message);
				}
				else
				{
					Received(message);
				}
			}
		}

		internal void OnError(Exception error)
		{
			if (Error != null)
			{
				if (_syncContext != null)
				{
					_syncContext.Post(err => Error((Exception)err), error);
				}
				else
				{
					Error(error);
				}
			}
		}

		internal void OnReconnected()
		{
			if (Reconnected != null)
			{
				if (_syncContext != null)
				{
					_syncContext.Post(_ => Reconnected(), null);
				}
				else
				{
					Reconnected();
				}
			}
		}

		internal void PrepareRequest(HttpWebRequest request)
		{
#if WINDOWS_PHONE
            // http://msdn.microsoft.com/en-us/library/ff637320(VS.95).aspx
            request.UserAgent = CreateUserAgentString("SignalR.Client.WP7");
#else
#if SILVERLIGHT
            // Useragent is not possible to set with Silverlight, not on the UserAgent property of the request nor in the Headers key/value in the request
#else
			request.UserAgent = CreateUserAgentString("SignalR.Client");
#endif
#endif
			if (Credentials != null)
			{
				request.Credentials = Credentials;
			}

			if (CookieContainer != null)
			{
				request.CookieContainer = CookieContainer;
			}
		}

		private static string CreateUserAgentString(string client)
		{
			if (_assemblyVersion == null)
			{
				_assemblyVersion = new AssemblyName(typeof(Connection).Assembly.FullName).Version;
			}

			return String.Format(CultureInfo.InvariantCulture, "{0}/{1} ({2})", client, _assemblyVersion, Environment.OSVersion);
		}

		private static bool TryParseVersion(string versionString, out Version version)
		{
			try
			{
				version = new Version(versionString);
				return true;
			}
			catch (ArgumentException)
			{
				version = new Version();
				return false;
			}
		}

		private static string CreateQueryString(IDictionary<string, string> queryString)
		{
			var stringList = new List<string>();
			foreach (var keyValue in queryString)
			{
				stringList.Add(keyValue.Key+"="+keyValue.Value);
			}
			return String.Join("&", stringList.ToArray());
		}
	}
}
