using System;
using System.IO;

namespace SignalR.Client._20.Infrastructure
{
    public interface IResponse
    {
        string ReadAsString();
        Stream GetResponseStream();
        void Close();

		bool IsFaulted { get; set; }
		Exception Exception { get; set; }
    }
}
