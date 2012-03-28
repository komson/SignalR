using System;
using System.IO;
using System.Net;

namespace SignalR.Client._20.Infrastructure
{
    public class HttpWebResponseWrapper : IResponse
    {
        private readonly HttpWebResponse _response;

        public HttpWebResponseWrapper(HttpWebResponse response)
        {
            _response = response;
        }

        public string ReadAsString()
        {
            return HttpHelper.ReadAsString(_response);
        }

        public Stream GetResponseStream()
        {
            return _response.GetResponseStream();
        }

        public void Close()
        {
            _response.Close();
        }

    	public bool IsFaulted { get; set; }

    	public Exception Exception { get; set; }
    }
}
