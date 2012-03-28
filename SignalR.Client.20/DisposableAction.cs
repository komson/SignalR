extern alias dotnet2;

using System;

namespace SignalR.Client._20 {
	internal class DisposableAction : IDisposable
	{
		private readonly dotnet2::System.Action _action;
		public DisposableAction(dotnet2::System.Action action)
		{
			_action = action;
		}

		public void Dispose()
		{
			_action();
		}
	}
}
