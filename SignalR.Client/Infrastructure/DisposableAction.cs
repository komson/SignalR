#if NET20
using SignalR.Client.Net20.Infrastructure;
#endif
using System;

namespace SignalR.Client.Infrastructure
{
    internal class DisposableAction : IDisposable
    {
        private readonly Action _action;
        public DisposableAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action();
        }
    }
}
