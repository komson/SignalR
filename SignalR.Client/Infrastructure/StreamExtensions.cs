using System;
using System.IO;
using System.Threading.Tasks;

namespace SignalR.Client.Infrastructure
{
    internal static class StreamExtensions
    {
        public static Task<int> ReadAsync(this Stream stream, byte[] buffer)
        {
#if NETFX_CORE
            return stream.ReadAsync(buffer, 0, buffer.Length);
#else
            try
            {
				return Task.Factory.FromAsync((cb, state) => stream.BeginRead(buffer, 0, buffer.Length, cb, state), ar => EndRead(stream, ar, buffer), null);
            }
            catch (Exception ex)
            {
                return TaskAsyncHelper.FromError<int>(ex);
            }
#endif
        }

        public static Task WriteAsync(this Stream stream, byte[] buffer)
        {
#if NETFX_CORE
            return stream.WriteAsync(buffer, 0, buffer.Length);
#else
            try
            {
                return Task.Factory.FromAsync((cb, state) => stream.BeginWrite(buffer, 0, buffer.Length, cb, state), ar => stream.EndWrite(ar), null);
            }
            catch (Exception ex)
            {
                return TaskAsyncHelper.FromError(ex);
            }
#endif
        }

    	private static int EndRead(Stream stream, IAsyncResult ar, byte[] buffer)
    	{
    		try
			{
				return stream.EndRead(ar);
    		}
    		catch (Exception)
    		{
    			return stream.ReadAsync(buffer).Result;
    		}
    	}
    }
}
