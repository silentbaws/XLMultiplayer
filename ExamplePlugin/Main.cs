using System;
using System.Threading.Tasks;
using XLMultiplayerServer;

namespace ExamplePlugin {
    public class Main {
		private static Plugin pluginInfo;

		public static void Load(Plugin info) {
			pluginInfo = info;
			pluginInfo.OnToggle = OnToggle;
			pluginInfo.OnConnect = OnConnection;
			pluginInfo.ReceiveUsername = OnUsername;
			pluginInfo.OnChatMessage = OnChatMessage;
			pluginInfo.ProcessMessage = ProccessMessage;
			pluginInfo.PlayerCommand = PlayerCommand;
		}

		private static void OnToggle(bool enabled) {
			if (enabled) {
				SayHi();
				KickRandom();
			}
		}

		private static bool OnConnection(string ip) {
			return true;
		}

		private static void OnUsername(PluginPlayer player, string name) {
			if(name.ToLower() == "silentbaws") {
				pluginInfo.SendServerAnnouncement("The god has arrived", 10, "0f0");
			}
		}

		private static bool OnChatMessage(PluginPlayer player, string message) {
			if(message.ToLower().Contains("racist stuff")) {
				return false;
			} else {
				return true;
			}
		}

		private static async void SayHi() {
			while (pluginInfo.enabled) {
				foreach (PluginPlayer p in pluginInfo.playerList) {
					byte[] b = new byte[] { 1, 2, 3, 4 };
					pluginInfo.SendMessage(pluginInfo, p.GetPlayer(), b, true);
					pluginInfo.SendImportantMessageToPlayer("Hello Friend", 10, "f00", p.GetPlayer());
				}

				pluginInfo.LogMessage("Hello friend", ConsoleColor.Green);
				await Task.Delay(5000);
			}
		}

		private static async void KickRandom() {
			while (pluginInfo.enabled) {
				if (pluginInfo.playerList.Count > 0) {
					Random random = new Random();
					int target = random.Next(0, pluginInfo.playerList.Count - 1);

					pluginInfo.DisconnectPlayer(pluginInfo.playerList[target].GetPlayer());
				}

				await Task.Delay(60000);
			}
		}

		private static void ProccessMessage(PluginPlayer sender, byte[] message) {
			string output = "Player " + sender.username + " sent bytes ";
			foreach (byte b in message) {
				output += b.ToString() + ", ";
			}
			pluginInfo.LogMessage(output, ConsoleColor.Blue);
		}

		private static bool PlayerCommand(string message, PluginPlayer sender) {
			if (message.ToLower().StartsWith("/prefix")) {
				string prefix = message.Remove(0, "/prefix ".Length);
				sender.AddUsernamePrefix(prefix);
				return true;
			}

			return false;
		}
    }
}
