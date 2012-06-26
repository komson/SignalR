using System;
using System.Net;

namespace SignalR.Client.Infrastructure
{
    internal static class ExceptionHelper
    {
        internal static bool IsRequestAborted(Exception exception)
        {
#if NET20
            var webException = ExceptionsExtensions.Unwrap(exception) as WebException;
#else
            var webException = exception.Unwrap() as WebException;
#endif
            return (webException != null && webException.Status == WebExceptionStatus.RequestCanceled);
        }
    }
}
