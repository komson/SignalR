using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SignalR.Client._20.Hubs;

namespace SignalR.Client._20.Samples
{
	class Program
	{
		static void Main(string[] args)
		{
			var hubConnection = new HubConnection("http://localhost:40476/");
			RunDemoHub(hubConnection);

			//RunStreamingSample();

			Console.ReadKey();
		}

		private static void RunDemoHub(HubConnection hubConnection)
		{
			var demo = hubConnection.CreateProxy("demo");

			demo.Subscribe("invoke").Data += i =>{
				Console.WriteLine("{0} client state index -> {1}", i[0], demo["index"]);
			};

			hubConnection.Start();


			demo.Invoke("multipleCalls");

			Thread.Sleep(7000);
			hubConnection.Stop();
		}

		private static void RunStreamingSample()
		{
			var connection = new Connection("http://localhost:40476/Raw/raw");

			connection.Received += data =>
			{
				Console.WriteLine(data);
			};

			connection.Reconnected += () =>
			{
				Console.WriteLine("[{0}]: Connection restablished", DateTime.Now);
			};

			connection.Error += e =>
			{
				Console.WriteLine(e);
			};

			connection.Start();
		}

		/*
		public class MyConnection : PersistentConnection
		{
			protected override Task OnConnectedAsync(Hosting.IRequest request, string connectionId)
			{
				Console.WriteLine("{0} Connected", connectionId);
				return base.OnConnectedAsync(request, connectionId);
			}

			protected override Task OnReceivedAsync(string connectionId, string data)
			{
				return Connection.Broadcast(data);
			}
		}
		 */
	}
}
