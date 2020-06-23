using System;
using System.Threading;
using System.Threading.Tasks;
using XLMultiplayerServer;

namespace XLMultiplayerServerConsoleApp {
	class ConsoleServer {
		public static int Main(String[] args) {
			Console.ForegroundColor = ConsoleColor.White;
			Console.BackgroundColor = ConsoleColor.Black;

			Server multiplayerServer = new Server(null, null);

			var serverTask = Task.Run(() => multiplayerServer.ServerLoop());

			Task.Run(() => multiplayerServer.CommandLoop());

			serverTask.Wait();
			return 0;
		}
	}
}
