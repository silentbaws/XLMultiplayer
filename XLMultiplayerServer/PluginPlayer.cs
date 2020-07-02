using System;
using System.Text;

namespace XLMultiplayerServer {
	public class PluginPlayer {
		private Player player;
		private Action<Player> OnUsernameUpdate;

		public byte playerID { get { return player.playerID; } private set { } }
		public string username { get { return player.username; } private set { } }
		public string currentVote { get { return player.currentVote; } private set { } }
		public byte[] loadedPlugins { get { return player.loadedPlugins.ToArray(); } private set { } }
		
		public PluginPlayer (Player referencingPlayer, Action<Player> usernameUpdateAction) {
			player = referencingPlayer;
			OnUsernameUpdate = usernameUpdateAction;
		}

		public string GetIPAddress() {
			return player.ipAddr.GetIP();
		}

		public void AddUsernamePrefix(string prefix) {
			player.username = prefix + player.username;
			UpdateUsernameMessage();
		}

		public void AddUsernamePostfix(string suffix) {
			player.username += suffix;
			UpdateUsernameMessage();
		}

		private void UpdateUsernameMessage() {
			byte[] sendMessage = new byte[username.Length + 2];
			sendMessage[0] = (byte)OpCode.Settings;
			Array.Copy(ASCIIEncoding.ASCII.GetBytes(username), 0, sendMessage, 1, username.Length);
			sendMessage[sendMessage.Length - 1] = player.playerID;

			player.usernameMessage = sendMessage;

			OnUsernameUpdate?.Invoke(player);
		}

		public void ClearVote() {
			player.currentVote = "current";
		}

		public Player GetPlayer() {
			return player;
		}
	}
}
