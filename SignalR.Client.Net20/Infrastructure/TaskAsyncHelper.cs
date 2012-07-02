using System;
using System.Threading;

namespace SignalR.Client.Net20.Infrastructure
{
    /// <summary>
    /// Static class with helper functions for the custom implementation of tasks.
    /// </summary>
    public static class TaskAsyncHelper
    {
        /// <summary>
        /// Create a task that is delayed by the given time span.
        /// </summary>
        /// <param name="timeSpan">The time span to delay subsequent operations with.</param>
        /// <returns>A non-generic task.</returns>
        public static Task Delay(TimeSpan timeSpan)
        {
            var newEvent = new Task();
			Timer timer = new Timer(_ => newEvent.OnFinished(null, null), null,
			timeSpan,
			TimeSpan.FromMilliseconds(-1));
        	newEvent.ContinueWith(_ => timer.Dispose());

			return newEvent;
        }

        /// <summary>
        /// Returns an empty task that is done.
        /// </summary>
        public static Task Empty
        {
            get
            {
                var task = new Task();
                task.OnFinished(null,null);
                return task;
            }
        }
    }

	public delegate void Action();
	public delegate void Action<T1, T2>(T1 arg1, T2 arg2);
	public delegate void Action<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3);
	public delegate void Action<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);

	public delegate TResult Func<TResult>();
	public delegate TResult Func<T, TResult>(T a);
	public delegate TResult Func<T1, T2, TResult>(T1 arg1, T2 arg2);
	public delegate TResult Func<T1, T2, T3, TResult>(T1 arg1, T2 arg2, T3 arg3);
	public delegate TResult Func<T1, T2, T3, T4, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
}