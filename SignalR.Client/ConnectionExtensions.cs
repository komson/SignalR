using System;
using Newtonsoft.Json;

namespace SignalR.Client
{
    public static class ConnectionExtensions
    {
#if NET20
        public static T GetValue<T>(IConnection connection, string key)
#else
        public static T GetValue<T>(this IConnection connection, string key)
#endif
        {
            object value;
            if (connection.Items.TryGetValue(key, out value))
            {
                return (T)value;
            }

            return default(T);
        }

#if !WINDOWS_PHONE && !SILVERLIGHT && !NET35 && !NET20
        public static IObservable<string> AsObservable(this Connection connection)
        {
            return connection.AsObservable(value => value);
        }

        public static IObservable<T> AsObservable<T>(this Connection connection)
        {
            return connection.AsObservable(value => JsonConvert.DeserializeObject<T>(value));
        }

        public static IObservable<T> AsObservable<T>(this Connection connection, Func<string, T> selector)
        {
            return new ObservableConnection<T>(connection, selector);
        }
#endif
    }
}
