using Newtonsoft.Json.Linq;
using System;
using SignalR.Client.Hubs;
using SignalR.Client.Infrastructure;
using SignalR.Client.Net20.Infrastructure;

namespace SignalR.Client.Net20.Hubs
{
	/// <summary>
	/// Extensions to the <see cref="IHubProxy"/>.
	/// </summary>
	public static class HubProxyExtensions
	{
		/// <summary>
		/// Gets the value of a state variable.
		/// </summary>
		/// <typeparam name="T">The type of the state variable</typeparam>
		/// <param name="proxy">The <see cref="IHubProxy"/>.</param>
		/// <param name="name">The name of the state variable.</param>
		/// <returns>The value of the state variable.</returns>
		public static T GetValue<T>(IHubProxy proxy, string name)
		{
			return Convert<T>(proxy[name]);
		}

		private static T Convert<T>(JToken obj)
		{
			if (obj == null)
			{
				return default(T);
			}

			return obj.ToObject<T>();
		}

		/// <summary>
		/// Registers for an event with the specified name and callback
		/// </summary>
		/// <param name="proxy">The <see cref="IHubProxy"/>.</param>
		/// <param name="eventName">The name of the event.</param>
		/// <param name="onData">The callback</param>
		/// <returns>An <see cref="IDisposable"/> that represents this subscription.</returns>
		public static IDisposable On(IHubProxy proxy, string eventName, Action onData)
		{
			Subscription subscription = proxy.Subscribe(eventName);

			Action<JToken[]> handler = args =>
			{
				onData();
			};

			subscription.Data += handler;

			return new DisposableAction(() => subscription.Data -= handler);
		}

		/// <summary>
		/// Registers for an event with the specified name and callback
		/// </summary>
		/// <param name="proxy">The <see cref="IHubProxy"/>.</param>
		/// <param name="eventName">The name of the event.</param>
		/// <param name="onData">The callback</param>
		/// <returns>An <see cref="IDisposable"/> that represents this subscription.</returns>
		public static IDisposable On<T>(IHubProxy proxy, string eventName, Action<T> onData)
		{
			Subscription subscription = proxy.Subscribe(eventName);

			Action<JToken[]> handler = args =>
			{
				onData(Convert<T>(args[0]));
			};

			subscription.Data += handler;

			return new DisposableAction(() => subscription.Data -= handler);
		}
	}
}
