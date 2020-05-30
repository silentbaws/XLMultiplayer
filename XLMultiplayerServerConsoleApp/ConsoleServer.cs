using System;
using System.Threading;
using System.Threading.Tasks;
using XLMultiplayerServer;

namespace XLMultiplayerServerConsoleApp {
	class ConsoleServer {
		public static void LogMessageCallbackHandler(string message, ConsoleColor color, params object[] objects) {
			Console.ForegroundColor = color;
			Console.WriteLine(message, objects);
			Console.ForegroundColor = ConsoleColor.White;
		}

		public static int Main(String[] args) {
			Console.ForegroundColor = ConsoleColor.White;
			Console.BackgroundColor = ConsoleColor.Black;

			LogMessage LogMessageCallback = LogMessageCallbackHandler;

			Server multiplayerServer = new Server(LogMessageCallback, null);
			FileServer fileServer = new FileServer(multiplayerServer);

			multiplayerServer.fileServer = fileServer;

			var serverTask = Task.Run(() => multiplayerServer.ServerLoop());

			Thread fileServerThread = new Thread(fileServer.ServerLoop);
			fileServerThread.IsBackground = true;
			fileServerThread.Start();

			Task.Run(() => multiplayerServer.CommandLoop());

			serverTask.Wait();
			return 0;
		}
	}
}
