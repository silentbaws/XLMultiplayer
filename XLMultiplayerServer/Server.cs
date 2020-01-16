using System;
using System.Threading;
using System.Threading.Tasks;
using Valve.Sockets;

namespace XLMultiplayerServer {
	class Server {
		public static bool RUNNING = true;
		public static ushort port = 7777;

		public static int Main(String[] args) {
			var serverTask = Task.Run(() => ServerLoop());
			Task.Run(() => CommandLoop());

			serverTask.Wait();
			return 0;
		}


		public static void CommandLoop() {
			while (RUNNING) {
				string input = Console.ReadLine();

				if(input.Equals("QUIT", StringComparison.CurrentCultureIgnoreCase)) {
					RUNNING = false;
				}

			}
		}

		public static void ServerLoop() {
			Library.Initialize();

			NetworkingSockets server = new NetworkingSockets();
			Address address = new Address();

			address.SetAddress("::0", port);

			uint listenSocket = server.CreateListenSocket(ref address);

			StatusCallback status = (info, context) => {
				switch (info.connectionInfo.state) {
					case ConnectionState.None:
						break;

					case ConnectionState.Connecting:
						server.AcceptConnection(info.connection);
						break;

					case ConnectionState.Connected:
						Console.WriteLine("Client connected - ID: " + info.connection + ", IP: " + info.connectionInfo.address.GetIP());
						break;

					case ConnectionState.ClosedByPeer:
						server.CloseConnection(info.connection);
						Console.WriteLine("Client disconnected - ID: " + info.connection + ", IP: " + info.connectionInfo.address.GetIP());
						break;
				}
			};
			const int maxMessages = 20;

			NetworkingMessage[] netMessages = new NetworkingMessage[maxMessages];

			while (RUNNING) {
				server.DispatchCallback(status);

				int netMessagesCount = server.ReceiveMessagesOnListenSocket(listenSocket, netMessages, maxMessages);

				if (netMessagesCount > 0) {
					for (int i = 0; i < netMessagesCount; i++) {
						ref NetworkingMessage netMessage = ref netMessages[i];

						Console.WriteLine("Message received from - ID: " + netMessage.connection + ", Channel ID: " + netMessage.channel + ", Data length: " + netMessage.length);

						netMessage.Destroy();
					}
				}
			}

			Library.Deinitialize();
		}
	}
}
