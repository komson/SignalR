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
}