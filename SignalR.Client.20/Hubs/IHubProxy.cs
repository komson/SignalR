using SignalR.Client._20.Transports;

namespace SignalR.Client._20.Hubs {
	public interface IHubProxy
	{
		object this[string name] { get; set; }

		Subscription Subscribe(string eventName);
		EventSignal<object> Invoke(string action, params object[] args);
		EventSignal<T> Invoke<T>(string action, params object[] args);
	}
}
