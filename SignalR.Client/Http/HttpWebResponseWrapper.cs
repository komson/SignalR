using System;
using System.IO;
using System.Net;
using SignalR.Client.Infrastructure;

namespace SignalR.Client.Http
{
    public class HttpWebResponseWrapper : IResponse
    {
        private readonly IRequest _request;
        private readonly HttpWebResponse _response;

        public HttpWebResponseWrapper(IRequest request, HttpWebResponse response)
        {
            _request = request;
            _response = response;
        }

        public string ReadAsString()
        {
#if NET20
            return HttpHelper.ReadAsString(_response);
#else
            return _response.ReadAsString();   
#endif
        }

        public Stream GetResponseStream()
        {
            return _response.GetResponseStream();
        }

        public void Close()
        {
            if (_request != null)
            {
                // Always try to abort the request since close hangs if the connection is 
                // being held open
                _request.Abort();
            }

            ((IDisposable)_response).Dispose();
        }
    }
}
