using System;
using System.IO;
using SignalR.Client._20.Transports;

namespace SignalR.Client._20
{
	internal static class StreamExtensions
	{
		public static EventSignal<CallbackDetail<int>> ReadAsync(Stream stream, byte[] buffer)
		{
			var signal = new EventSignal<CallbackDetail<int>>(30);
            try
            {
            	var state = new StreamState{Stream= stream,Response = signal};
                stream.BeginRead(buffer, 0, buffer.Length, GetResponseCallback, state);
            }
			catch(Exception exception)
			{
				signal.OnFinish(new CallbackDetail<int>{IsFaulted = true,Exception = exception});
			}
			return signal;
		}

		private static void GetResponseCallback(IAsyncResult asynchronousResult)
		{
			StreamState streamState = (StreamState)asynchronousResult.AsyncState;

			// End the operation
			try
			{
				var response = streamState.Stream.EndRead(asynchronousResult);
				streamState.Response.OnFinish(new CallbackDetail<int> { Result = response });
			}
			catch (Exception ex)
			{
				streamState.Response.OnFinish(new CallbackDetail<int>{IsFaulted = true,Exception = ex});
			}
		}
	}

	internal class StreamState
	{
		public Stream Stream { get; set; }

		public EventSignal<CallbackDetail<int>> Response { get; set; }
	}
}
