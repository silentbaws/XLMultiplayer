using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExamplePlugin {
    public class Main {
		private static XLMultiplayerServer.Server gameplayServer;

		public static void Load(XLMultiplayerServer.Server server) {
			gameplayServer = server;
			SayHi();
		}

		private static async void SayHi() {
			while (true) {
				gameplayServer.LogMessageCallback("Hello friend", ConsoleColor.Green);
				await Task.Delay(5000);
			}
		}
    }
}
