﻿using System;

namespace SignalR.Client.Net20.Infrastructure
{
    /// <summary>Provides a mechanism for receiving push-based notifications.</summary>
    /// <remarks>This is here because this interface was introduced in a later version of the framework.</remarks>
    public interface IObserver<T>
    {
        void OnNext(T value);
        void OnCompleted();

        /// <summary>
        /// Called upon exception.
        /// </summary>
        /// <param name="exception">The exception details.</param>
        void OnError(Exception exception);
    }
}
