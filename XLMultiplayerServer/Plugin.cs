using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace XLMultiplayerServer {

	public class Plugin {
		[JsonRequired]
		[JsonProperty("StartupMethod")]
		public string startMethod { get; private set; } = "";

		[JsonRequired]
		[JsonProperty("AssemblyName")]
		public string dllName { get; private set; } = "";

		[JsonRequired]
		[JsonProperty("Name")]
		public string name { get; private set; } = "";

		[JsonRequired]
		[JsonProperty("ServerVersion")]
		public string serverVersion { get; private set; } = "";

		[JsonProperty("DependencyZIP")]
		public string dependencyFile { get; private set; } = "";

		public string hash { get; private set; } = "";

		public string path { get; private set; } = "";

		public byte pluginID { get; private set; } = 255;
		
		public bool enabled { get; private set; } = false;
		public byte maxPlayers { get; private set; }

		public string currentMap = "";

		private List<Tuple<Player, byte[]>> outboundMessages = new List<Tuple<Player, byte[]>>();

		public LogMessage LogMessage { get; private set; }
		public Action<string, int, string> SendServerAnnouncement { get; private set; }
		public Action<string> ChangeMap { get; private set; }
		public Action<Plugin, Player, byte[], bool> SendMessage { get; private set; }
		public Action<Player> DisconnectPlayer { get; private set; }
		public Action<string, int, string, Player> SendImportantMessageToPlayer { get; private set; }

		public List<Player> playerList = new List<Player>();

		public Dictionary<string, string> mapList = new Dictionary<string, string>();

		public Action<bool> OnToggle;
		public Action<string> ServerCommand;
		public Action<string, Player> PlayerCommand;
		public Action<Player, byte[]> ProcessMessage;
		public Action<Player, string> ReceiveUsername;
		public Action<string, byte> OnDisconnect;
		public Func<string, bool> OnConnect;
		public Func<Player, string, bool> OnChatMessage;

		public Plugin(string PluginName, string PluginDLL, string PluginStartMethod, string Dependency, string version, string PluginPath, byte ID, 
			LogMessage MessageCallback, Action<string, int, string> AnnouncementCallback, Action<string> MapChangeCallback, Action<Plugin, Player, byte[], bool> Send, 
			Action<Player> disconnect, Action<string, int, string, Player> sendImportant) {
			name = PluginName;
			dllName = PluginDLL;
			startMethod = PluginStartMethod;
			path = PluginPath;
			serverVersion = version;
			pluginID = ID;
			LogMessage = MessageCallback;
			SendServerAnnouncement = AnnouncementCallback;
			ChangeMap = MapChangeCallback;
			SendMessage = Send;
			DisconnectPlayer = disconnect;
			dependencyFile = Dependency;
			SendImportantMessageToPlayer = sendImportant;
			
			if (dependencyFile != "" && PluginPath != null && File.Exists(Path.Combine(path, dependencyFile))) {
				dependencyFile = Path.Combine(path, dependencyFile);
				hash = Server.CalculateMD5(dependencyFile);
			}
		}

		public Tuple<Player, byte[]>[] GetMessageQueue() {
			Tuple<Player, byte[]>[] returnVal = outboundMessages.ToArray();
			outboundMessages.Clear();

			return returnVal;
		}

		public void TogglePlugin(bool enabled) {
			if (this.enabled == enabled) return;
			this.enabled = enabled;
			OnToggle?.Invoke(enabled);
		}
	}

}
