#define VALVESOCKETS_SPAN
#define VALVESOCKETS_INLINING

using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Valve.Sockets;

// TODO: Keep track of all connection uints and close unused connections

namespace XLMultiplayerServer {
	public enum OpCode : byte {
		Connect = 0,
		Settings = 1,
		VersionNumber = 2,
		Animation = 3,
		Texture = 4,
		Chat = 5,
		UsernameAdjustment = 6,
		MapHash = 7,
		MapVote = 8,
		MapList = 9,
		ServerMessage = 10,
		Sound = 11,
		StillAlive = 254,
		Disconnect = 255
	}

	public enum MPTextureType : byte {
		Shirt = 0,
		Pants = 1,
		Shoes = 2,
		Hat = 3,
		Deck = 4,
		Grip = 5,
		Trucks = 6,
		Wheels = 7,
		Head = 8,
		Body = 9
	}

	public delegate void LogMessage(string message, ConsoleColor textColor, params object[] objects);
	public delegate void LogChatMessage(string message);

	public class Plugin {
		[JsonRequired]
		[JsonProperty("StartupMethod")]
		public string startMethod = "";

		[JsonRequired]
		[JsonProperty("AssemblyName")]
		public string dllName = "";

		[JsonRequired]
		[JsonProperty("Name")]
		public string name = "";

		public string path = "";
	}

	public class Server {
		// TODO: Update version number with versions
		private string VERSION_NUMBER = "0.9.0";

		public LogMessage LogMessageCallback;
		public LogChatMessage LogChatMessageCallback;

		public NetworkingSockets server;
		public FileServer fileServer;
		private uint pollGroup;
		private uint listenSocket;
		public bool RUNNING { get; private set; } = true;

		[JsonProperty("Server_Name")]
		public static string SERVER_NAME { get; private set; } = "";

		[JsonProperty("Port")]
		public static ushort port { get; private set;  } = 7777;

		[JsonProperty("Max_Players")]
		public static byte MAX_PLAYERS { get; private set; } = 10;

		[JsonProperty("Enforce_Map")]
		private static bool ENFORCE_MAPS = true;

		[JsonProperty("MessageOTD")]
		private static string defaultMOTD = "";

		[JsonProperty("API_Key")]
		private static string API_KEY = "";

		[JsonProperty("Maps_Folder")]
		private static string mapsDir = "";

		[JsonProperty("Paypal_Link")]
		public static string PAYPAL { get; private set; } = "";

		[JsonProperty("Max_Upload_Bytes_Per_Second")]
		private static int MAX_UPLOAD = 15400000;

		[JsonProperty("Max_Queued_Sending_Bytes")]
		private static int MAX_BUFFER = 209715200;

		[JsonProperty("Max_Queued_Bytes_Per_Connection")]
		private static int MAX_BYTES_PENDING = 102400;

		[JsonProperty("File_Max_Upload_Bytes_Per_Second")]
		public static int FILE_MAX_UPLOAD = 4000000;

		public Player[] players { get; private set; }
		private int total_players = 0;

		private string sep = Path.DirectorySeparatorChar.ToString();
		
		private byte[] mapListBytes = null;
		public Dictionary<string, string> mapList { get; private set; } = new Dictionary<string, string>();
		private Dictionary<string, int> mapVotes = new Dictionary<string, int>();

		private Stopwatch mapVoteTimer = new Stopwatch();

		public string currentMapHash { get; private set; } = "1";

		public List<string> bannedIPs = new List<string>();
		public byte[] motdBytes = null;
		public string motdString = "";

		private List<Plugin> loadedPlugins = new List<Plugin>();

		public Server(LogMessage logCallback, LogChatMessage logChatCallback) {
			LogMessageCallback = logCallback;
			LogChatMessageCallback = logChatCallback;
		}

		public static string CalculateMD5(string filename) {
			using (var md5 = MD5.Create()) {
				using (var stream = File.OpenRead(filename)) {
					byte[] hash = null;
					long size = new FileInfo(filename).Length;
					if (size > 10485760) {
						byte[] bytes = new byte[10485760];
						stream.Read(bytes, 0, 10485760);
						hash = md5.ComputeHash(bytes);
					} else {
						hash = md5.ComputeHash(stream);
					}
					return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
				}
			}
		}

		public void LoadMapList() {
			if (!mapVoteTimer.IsRunning) {
				byte[] oldBytes = mapListBytes;
				mapListBytes = GenerateMapList();

				if (mapListBytes == null) {
					mapListBytes = oldBytes;
					return;
				}

				foreach (Player player in players) {
					if(player != null) {
						server.SendMessageToConnection(player.connection, mapListBytes);
						player.currentVote = "current";
					}
				}
			} else {
				LogMessageCallback("You can't reload the maps during a vote to change", ConsoleColor.Red);
			}
		}

		private byte[] GenerateMapList() {
			mapList.Clear();

			mapList.Add("0", "Courthouse");
			mapList.Add("1", "California Skatepark");
			mapList.Add("2", "Miniramp - test");

			if (mapsDir == "") {
				mapsDir = Directory.GetCurrentDirectory() + sep + "Maps";
				LogMessageCallback($"No maps folder set so using {mapsDir}", ConsoleColor.White);
			}

			if (Directory.Exists(mapsDir)) {
				string[] files = Directory.GetFiles(mapsDir);
				if (files.Length < 1) {
					LogMessageCallback("\nWARNING: FAILED TO FIND ANY CUSTOM MAPS IN THE MAPS DIRECTORY ONLY COURTHOUSE AND CALIFORNIA WILL BE USED\n", ConsoleColor.Yellow);
				} else {
					foreach (string file in files) {
						string hash = CalculateMD5(file);
						try {
							mapList.Add(hash, Path.GetFileName(file));
							LogMessageCallback("Adding map: " + Path.GetFileName(file) + ", with hash: " + hash + " to servers map list\n", ConsoleColor.White);
						} catch (ArgumentException) {
							LogMessageCallback("**WARNING** MAP " + Path.GetFileName(file) + " HASH OVERLAPS WITH " + mapList[hash] + " PLEASE DM ME TO LET ME KNOW\n", ConsoleColor.Yellow);
						}
					}
					LogMessageCallback("Finished hashing maps", ConsoleColor.White);
				}
			} else {
				LogMessageCallback("\nWARNING: FAILED TO FIND MAPS DIRECTORY \"{0}\" SO ONLY COURTHOUSE AND CALIFORNIA WILL BE USED\n", ConsoleColor.Yellow, mapsDir);
			}

			byte[] mapListBytes = GetMapListBytes();
			return mapListBytes;
		}

		private byte[] GetMapListBytes() {
			List<byte> mapListBytes = new List<byte>();

			mapListBytes.Add((byte)OpCode.MapList);

			foreach (var item in mapList) {
				byte[] hashLength = BitConverter.GetBytes(item.Key.Length);
				byte[] hashBytes = ASCIIEncoding.ASCII.GetBytes(item.Key);
				byte[] nameLength = BitConverter.GetBytes(item.Value.Length);
				byte[] nameBytes = ASCIIEncoding.ASCII.GetBytes(item.Value);

				mapListBytes.AddRange(hashLength);
				mapListBytes.AddRange(hashBytes);
				mapListBytes.AddRange(nameLength);
				mapListBytes.AddRange(nameBytes);
			}

			return mapListBytes.ToArray();
		}

		private byte[] GetCurrentMapHashMessage() {
			byte[] mapHashBytes = ASCIIEncoding.ASCII.GetBytes(currentMapHash);
			byte[] mapNameBytes = ASCIIEncoding.ASCII.GetBytes(mapList[currentMapHash]);

			byte[] newMapMessage = new byte[mapHashBytes.Length + mapNameBytes.Length + 5];

			newMapMessage[0] = (byte)OpCode.MapHash;
			Array.Copy(BitConverter.GetBytes(mapNameBytes.Length), 0, newMapMessage, 1, 4);
			Array.Copy(mapNameBytes, 0, newMapMessage, 5, mapNameBytes.Length);
			Array.Copy(mapHashBytes, 0, newMapMessage, 5 + mapNameBytes.Length, mapHashBytes.Length);

			return newMapMessage;
		}

		public void CommandLoop() {
			while (RUNNING) {
				string input = Console.ReadLine();

				if(input.Equals("QUIT", StringComparison.CurrentCultureIgnoreCase)) {
					RUNNING = false;
				}else if (input.ToLower().StartsWith("kick")) {
					string kickIDString = input.ToLower().Replace("kick ", "");
					int kickID = -1;
					if (Int32.TryParse(kickIDString, out kickID)) {
						if (players[kickID] != null) {
							LogMessageCallback("Kicking player {0}", ConsoleColor.White, kickID);
							RemovePlayer(players[kickID].connection, kickID, true);
						}
					} else {
						LogMessageCallback("Invalid player ID", ConsoleColor.White);
					}
				} else if (input.ToLower().StartsWith("ban")) {
					string banIDString = input.ToLower().Replace("ban ", "");
					int banID = -1;
					if (Int32.TryParse(banIDString, out banID)) {
						BanPlayer(banID);
					} else {
						LogMessageCallback("Invalid player ID", ConsoleColor.White);
					}
				} else if (input.ToLower().StartsWith("msg")) {
					byte[] messageBytes = ProcessMessageCommand(input);
					if (messageBytes != null) {
						foreach(Player player in players) {
							if(player != null) {
								fileServer.server.SendMessageToConnection(player.fileConnection, messageBytes, SendFlags.Reliable);
							}
						}
					}
				} else if (input.ToLower().StartsWith("motd")) {
					byte[] messageBytes = ProcessMessageCommand(input);
					if (messageBytes != null) {
						motdBytes = messageBytes;
					}
				}
			}
		}

		public byte[] ProcessMessageCommand(string input) {
			//msg:duration:color content of message here
			//   - duration/color optional parameters so default duration is 10 seconds, default color ff00ff
			string[] msgParams = input.Split(new char[] { ' ' }, 2);

			if (msgParams.Length < 2) return null;

			string[] msgSettings = msgParams[0].Split(':');

			int duration = 10;
			string msgColor = "ff00ff";

			string acceptableCharacters = "abcdef0123456789";

			if (msgSettings.Length > 1) {
				if (Int32.TryParse(msgSettings[1], out duration)) {
					duration = Math.Min(duration, 60);
					duration = Math.Max(duration, 5);
				} else {
					duration = 10;
				}
			}
			if (msgSettings.Length > 2) {
				bool validColor = true;

				foreach (char c in msgSettings[2]) {
					if (!acceptableCharacters.Contains(c.ToString())) {
						validColor = false;
					}
				}
				validColor = validColor && (msgSettings[2].Length == 3 || msgSettings[2].Length == 6);

				if (validColor) {
					msgColor = msgSettings[2];
				}
			}


			if (input.ToLower().StartsWith("motd")) {
				motdString = RemoveMarkup(msgParams[1]);
			}

			string messageText = $"<b><color=#{msgColor}>{RemoveMarkup(msgParams[1])}</color></b>";
			byte[] messageBytes = new byte[messageText.Length + 5];
			messageBytes[0] = (byte)OpCode.ServerMessage;
			Array.Copy(BitConverter.GetBytes(duration * 1000), 0, messageBytes, 1, 4);
			Array.Copy(ASCIIEncoding.ASCII.GetBytes(messageText), 0, messageBytes, 5, messageText.Length);

			return messageBytes;
		}

		public void ProcessMessage(byte[] buffer, byte fromID, NetworkingSockets server) {
			if(!Enum.IsDefined(typeof(OpCode), (OpCode)buffer[0]) || players[fromID] == null) {
				return;
			}

			players[fromID].timeoutWatch.Restart();

			switch ((OpCode)buffer[0]) {
				case OpCode.Settings:
					Thread usernameThread = new Thread(new ParameterizedThreadStart(HandleUsername)) {
						IsBackground = true
					};
					usernameThread.Start(Tuple.Create(buffer, fromID));
					break;
				case OpCode.Texture: {
						LogMessageCallback("Received Texture from " + fromID, ConsoleColor.White);
						Player player = players[fromID];
						if (player != null && player.playerID == fromID) {
							player.AddGear(buffer);
							if (player.allGearUploaded) {
								foreach (Player player2 in players) {
									if (player2 != null && player2.playerID != fromID) {
										foreach (KeyValuePair<string, byte[]> value in player.gear) {
											fileServer.server.SendMessageToConnection(player2.fileConnection, value.Value, SendFlags.Reliable);
										}
										fileServer.server.FlushMessagesOnConnection(player2.fileConnection);
									}
								}
							}
						}
					}
					break;
				case OpCode.Animation:
					if (players[fromID] != null) {
						bool reliable = buffer[buffer.Length - 1] == (byte)1 ? true : false;
						buffer[buffer.Length - 1] = fromID;

						foreach (Player player in players) {
							if (player != null && player.playerID != fromID) {
								ConnectionStatus status = new ConnectionStatus();
								if (server.GetQuickConnectionStatus(player.connection, ref status)) {
									int bytesPending = status.pendingReliable + status.sentUnackedReliable;

									if (reliable && bytesPending >= MAX_BYTES_PENDING) {
										LogMessageCallback($"Sending animation unreliably to ({player.playerID}) because pending bytes is higher than max", ConsoleColor.White);
										reliable = false;
									}
								}

								server.SendMessageToConnection(player.connection, buffer, reliable ? SendFlags.Reliable | SendFlags.NoNagle : SendFlags.Unreliable | SendFlags.NoNagle);
							}
						}
					}
					break;
				case OpCode.Sound:
					byte[] newBuffer = new byte[buffer.Length + 1];
					Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
					newBuffer[buffer.Length] = fromID;

					foreach (Player player in players) {
						if (player != null && player.playerID != fromID) {
							server.SendMessageToConnection(player.connection, newBuffer, SendFlags.Reliable);
						}
					}
					break;
				case OpCode.Chat:
					string contents = ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 1);
					LogMessageCallback("Chat Message from {0} saying: {1}", ConsoleColor.White, fromID, contents);
					LogChatMessageCallback?.Invoke($"{RemoveMarkup(players[fromID].username)}({fromID}): {contents}");

					byte[] sendBuffer = new byte[buffer.Length + 1];
					Array.Copy(buffer, 0, sendBuffer, 0, buffer.Length);
					sendBuffer[buffer.Length] = fromID;
					
					if (contents.StartsWith("/")) {
						if (contents.StartsWith("/report ", StringComparison.CurrentCultureIgnoreCase)) {
							string[] splitContents = contents.Split(new char[] { ' ' }, 2);
							if (splitContents.Length == 2) {
								int reportedID = -1;
								if (Int32.TryParse(splitContents[1], out reportedID)) {
									if (players[reportedID] != null) {
										StreamWriter writer = new StreamWriter($"Report {((ulong)DateTime.Now.ToBinary()).ToString()}.txt");
										writer.WriteLine($"Player {RemoveMarkup(players[reportedID].username)} with the IP: {players[reportedID].ipAddr.GetIP()} was reported following these messages");
										foreach (string msg in players[reportedID].previousMessages) {
											writer.WriteLine(msg);
											writer.Flush();
										}
										writer.Close();

										byte[] response = ProcessMessageCommand($"msg:5:ff0 Successfully reported player {players[reportedID].username}");
										server.SendMessageToConnection(players[fromID].connection, response, SendFlags.Reliable);
										break;
									}
								}
							}

							byte[] invalidReport = ProcessMessageCommand($"msg:5:f00 Improperly formatted report message or player with given ID does not exist");
							server.SendMessageToConnection(players[fromID].connection, invalidReport, SendFlags.Reliable);
							break;
						}

						byte[] invalidCommand = ProcessMessageCommand($"msg:5:f00 Given command does not exist");
						server.SendMessageToConnection(players[fromID].connection, invalidCommand, SendFlags.Reliable);
						break;
					} else {
						players[fromID].previousMessages.Add(contents);
						if (players[fromID].previousMessages.Count > 10) players[fromID].previousMessages.RemoveAt(0);
						foreach (Player player in players) {
							if (player != null) {
								server.SendMessageToConnection(player.connection, sendBuffer, SendFlags.Reliable);
							}
						}
					}

					break;
				case OpCode.MapVote:
					if (ENFORCE_MAPS) {
						string vote = ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 1);

						if(mapList.ContainsKey(vote) || vote.ToLower().Equals("current")) {
							vote = mapList.ContainsKey(vote) && mapList[vote].Equals(currentMapHash) ? "current" : vote;
							players[fromID].currentVote = vote;

							LogMessageCallback("{0} voted for the map {1}", ConsoleColor.White, fromID, mapList.ContainsKey(vote) ? mapList[vote] : vote);
						}
					}
					break;
				case OpCode.StillAlive:
					if (players[fromID] != null) server.SendMessageToConnection(players[fromID].connection, buffer, SendFlags.Unreliable | SendFlags.NoNagle);
					break;
			}
		}

		public void StatusCallbackFunction(ref StatusInfo info, IntPtr context) {
			switch (info.connectionInfo.state) {
				case ConnectionState.None:
					break;

				case ConnectionState.Connecting: {
						if (info.connectionInfo.listenSocket != listenSocket && info.connectionInfo.listenSocket == fileServer.listenSocket) {
							fileServer.StatusCallbackFunction(ref info, context);
							break;
						} else if (info.connectionInfo.listenSocket != listenSocket) {
							LogMessageCallback("refusing connection invalid listen socket", ConsoleColor.White);
							server.CloseConnection(info.connection);
							break;
						}

						LogMessageCallback("connecting on game server", ConsoleColor.White);

						if (bannedIPs.Contains(info.connectionInfo.address.GetIP())) {
							LogMessageCallback("Ban player attempted to connect to the server, IP: {0}", ConsoleColor.White, info.connectionInfo.address.GetIP());
							server.CloseConnection(info.connection);
						} else {
							server.AcceptConnection(info.connection);
							server.SetConnectionPollGroup(pollGroup, info.connection);
						}
					}
					break;

				case ConnectionState.Connected: {
						if (info.connectionInfo.listenSocket != listenSocket && info.connectionInfo.listenSocket == fileServer.listenSocket) {
							fileServer.StatusCallbackFunction(ref info, context);
							break;
						} else if (info.connectionInfo.listenSocket != listenSocket) {
							LogMessageCallback("refusing connection invalid listen socket", ConsoleColor.White);
							server.CloseConnection(info.connection);
							break;
						}
						LogMessageCallback("connected on game server", ConsoleColor.White);

						bool openSlot = false;

						for (byte i = 0; i < MAX_PLAYERS; i++) {
							if (players[i] == null) {
								LogMessageCallback("Client connected - IP: " + info.connectionInfo.address.GetIP() + " ID: " + i.ToString(), ConsoleColor.White);
								players[i] = new Player(i, info.connection, info.connectionInfo.address);

								byte[] versionNumber = ASCIIEncoding.ASCII.GetBytes(VERSION_NUMBER);
								byte[] versionMessage = new byte[versionNumber.Length + 5];

								versionMessage[0] = (byte)OpCode.VersionNumber;
								Array.Copy(BitConverter.GetBytes(players[i].connection), 0, versionMessage, 1, 4);
								Array.Copy(versionNumber, 0, versionMessage, 5, versionNumber.Length);
								server.SendMessageToConnection(players[i].connection, versionMessage, SendFlags.Reliable | SendFlags.NoNagle);

								if (ENFORCE_MAPS) {
									server.SendMessageToConnection(players[i].connection, mapListBytes, SendFlags.Reliable);
									server.SendMessageToConnection(players[i].connection, GetCurrentMapHashMessage(), SendFlags.Reliable);
								}

								foreach (Player player in players) {
									if (player != null && player != players[i]) {
										server.SendMessageToConnection(players[i].connection, new byte[] { (byte)OpCode.Connect, player.playerID }, SendFlags.Reliable);
										server.SendMessageToConnection(player.connection, new byte[] { (byte)OpCode.Connect, i }, SendFlags.Reliable);

										if (player.usernameMessage != null) {
											server.SendMessageToConnection(players[i].connection, player.usernameMessage, SendFlags.Reliable);
										}
									}
								}

								server.FlushMessagesOnConnection(players[i].connection);

								openSlot = true;
								break;
							}
						}

						if (!openSlot) {
							server.CloseConnection(info.connection);
						}
					}
					break;

				case ConnectionState.ClosedByPeer:
				case ConnectionState.ProblemDetectedLocally:
					RemovePlayer(info.connection);
					break;
			}
		}

		public void MessageCallbackFunction(in NetworkingMessage netMessage) {
			byte[] messageData = new byte[netMessage.length];
			netMessage.CopyTo(messageData);

			Player sendingPlayer = null;
			foreach (Player player in players) {
				if (player != null && player.connection == netMessage.connection) {
					sendingPlayer = player;
					break;
				}
			}

			if (sendingPlayer != null)
				ProcessMessage(messageData, sendingPlayer.playerID, server);
		}

		public void BanPlayer(int banID) {
			if (players[banID] != null) {
				LogMessageCallback("Banning player {0} IP {1}", ConsoleColor.White, banID, players[banID].ipAddr.GetIP());
				bannedIPs.Add(players[banID].ipAddr.GetIP());
				RemovePlayer(players[banID].connection, banID, true);

				string ipBanString = "";
				foreach (string ip in bannedIPs) {
					ipBanString += ip + "\n";
				}

				File.WriteAllText("ipban.txt", ipBanString);
			}
		}
		
		public void RemoveBan(string ip) {
			LogMessageCallback("Removing ban for IP {0}", ConsoleColor.White, ip);
			bannedIPs.Remove(ip);

			string ipBanString = "";
			foreach (string bannedIP in bannedIPs) {
				ipBanString += bannedIP + "\n";
			}

			File.WriteAllText("ipban.txt", ipBanString);
		}

		public void RemovePlayer(uint connection, int ID = -1, bool kicked = false) {
			Player removedPlayer = null;
			if (ID == -1) {
				foreach (Player player in players) {
					if (player != null && (player.connection == connection || player.fileConnection == connection)) {
						removedPlayer = player;
						break;
					}
				}
			} else {
				removedPlayer = players[ID];
			}

			if (removedPlayer == null) return;

			server.CloseConnection(removedPlayer.connection);
			fileServer.server.CloseConnection(removedPlayer.fileConnection);

			if (removedPlayer != null) {
				if (!kicked) LogMessageCallback("Client disconnected - ID: " + removedPlayer.playerID, ConsoleColor.White);
				foreach (Player player in players) {
					if (player != null && player != removedPlayer) {
						server.SendMessageToConnection(player.connection, new byte[] { (byte)OpCode.Disconnect, removedPlayer.playerID }, SendFlags.Reliable | SendFlags.NoNagle);
					}
				}
				players[removedPlayer.playerID] = null;
			}
		}

		private void LoadServerPlugins() {
			string path = Directory.GetCurrentDirectory() + sep + "Plugins";
			if (Directory.Exists(path)) {
				foreach(string dir in Directory.GetDirectories(path)) {
					if(File.Exists(dir + sep + "info.json")) {
						Console.WriteLine("Found plugin");
						Plugin newPlugin = JsonConvert.DeserializeObject<Plugin>(File.ReadAllText(dir + sep + "info.json"));
						if (newPlugin != null && newPlugin.dllName != "" && newPlugin.startMethod != "") {
							newPlugin.path = dir + sep;
							loadedPlugins.Add(newPlugin);
						}
					}
				}
			}

			foreach (Plugin plugin in loadedPlugins) {
				string dll = plugin.dllName;

				var loadedDLL = Assembly.LoadFile(plugin.path + dll);

				Console.WriteLine(plugin.path + dll);
				if (loadedDLL != null) {
					MethodInfo entryMethod = AccessTools.Method(plugin.startMethod);

					if (entryMethod != null) {
						try {
							//new object[] { this }
							entryMethod.Invoke(null, new object[] { this });
						} catch (Exception e) {
							LogMessageCallback($"Exception calling entry method of plugin {plugin.name}: " + e.ToString(), ConsoleColor.Red);
						}
					} else {
						LogMessageCallback($"Specified entry method of plugin {plugin.name} could not be found", ConsoleColor.Red);
					}
				} else {
					LogMessageCallback($"DLL for plugin {plugin.name} could not be found", ConsoleColor.Red);
				}
			}
		}

		public void ServerLoop() {
			LogMessageCallback("Starting server initialization", ConsoleColor.White);

			if (File.Exists(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar.ToString() + "ServerConfig.json")) {
				JsonConvert.DeserializeObject<Server>(File.ReadAllText(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar.ToString() + "ServerConfig.json"));
				players = new Player[MAX_PLAYERS];
			} else {
				LogMessageCallback("Could not find server config file", ConsoleColor.White);
				Console.In.Read();
				return;
			}

			players = new Player[MAX_PLAYERS];

			LoadMapList();
			if (mapListBytes == null && ENFORCE_MAPS) return;

			if (!defaultMOTD.Equals("")) {
				byte[] defaultMOTDBytes = ProcessMessageCommand(defaultMOTD);
				if (defaultMOTDBytes != null) {
					motdBytes = defaultMOTDBytes;
				}
			}

			if (File.Exists("ipban.txt")) {
				string ipBanList = File.ReadAllText("ipban.txt");
				string[] readBannedIPs = ipBanList.Split('\n');
				bannedIPs.AddRange(readBannedIPs);
			}

			LogMessageCallback("Finished server initialization", ConsoleColor.White);

			LogMessageCallback("Loading plugins", ConsoleColor.White);

			LoadServerPlugins();

			LogMessageCallback("Finished loading plugins", ConsoleColor.White);

			Library.Initialize();

			server = new NetworkingSockets();
			Address address = new Address();
			
			NetworkingUtils utils = new NetworkingUtils();

			utils.SetDebugCallback(DebugType.Important, (type, message) => {
				LogMessageCallback("Valve Debug - Type: {0}, Message: {1}", ConsoleColor.White, type, message);
			});
			
			address.SetAddress("::0", port);

			listenSocket = server.CreateListenSocket(ref address);
			pollGroup = server.CreatePollGroup();
			
			unsafe {
				int sendRateMin = 600000;
				int sendRateMax = MAX_UPLOAD;
				int sendBufferSize = MAX_BUFFER;
				
				utils.SetConfigurationValue(ConfigurationValue.SendRateMin, ConfigurationScope.ListenSocket, new IntPtr(listenSocket), ConfigurationDataType.Int32, new IntPtr(&sendRateMin));
				utils.SetConfigurationValue(ConfigurationValue.SendRateMax, ConfigurationScope.ListenSocket, new IntPtr(listenSocket), ConfigurationDataType.Int32, new IntPtr(&sendRateMax));
				utils.SetConfigurationValue(ConfigurationValue.SendBufferSize, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendBufferSize));
			}

			LogMessageCallback($"Server {SERVER_NAME} started Listening on port {port} for maximum of {MAX_PLAYERS} players\nEnforcing maps is {ENFORCE_MAPS}", ConsoleColor.White);

			StartAnnouncing();

			StatusCallback status = StatusCallbackFunction;

#if VALVESOCKETS_SPAN
			MessageCallback messageCallback = MessageCallbackFunction;
#else
			const int maxMessages = 256;

			NetworkingMessage[] netMessages = new NetworkingMessage[maxMessages];
#endif
			while (RUNNING) {
				server.DispatchCallback(status);

#if VALVESOCKETS_SPAN
				server.ReceiveMessagesOnPollGroup(pollGroup, messageCallback, 256);
#else
				int netMessagesCount = server.ReceiveMessagesOnConnection(listenSocket, netMessages, maxMessages);

				if (netMessagesCount > 0) {
					for (int i = 0; i < netMessagesCount; i++) {
						ref NetworkingMessage netMessage = ref netMessages[i];

						byte[] messageData = new byte[netMessage.length];
						netMessage.CopyTo(messageData);

						Player sendingPlayer = null;
						foreach (Player player in players) {
							if (player != null && player.connection == netMessage.connection) {
								sendingPlayer = player;
								break;
							}
						}

						//LogMessageCallback("Recieved packet from connection {0}, sending player null: {1}", netMessage.connection, sendingPlayer == null);

						if (sendingPlayer != null)
							ProcessMessage(messageData, sendingPlayer.playerID, server);

						netMessage.Destroy();
					}
				}
#endif

				mapVotes.Clear();
				total_players = 0;
				foreach (Player player in players) {
					if (player != null) {
						total_players++;

						if (player.timeoutWatch.ElapsedMilliseconds > 15000) {
							LogMessageCallback($"{player.playerID} has been timed out for not responding for 15 seconds", ConsoleColor.White);

							RemovePlayer(player.connection, player.playerID, true);
						}

						if (!mapVotes.ContainsKey(player.currentVote)) {
							mapVotes.Add(player.currentVote, 1);
						} else {
							mapVotes[player.currentVote] ++;
						}
					}
				}

				// Handle map voting and map enforcement
				if (ENFORCE_MAPS) {
					if (total_players == 0) {
						currentMapHash = "1";
					}

					bool startNewTimer = false;
					if (mapVotes.ContainsKey("current")) {
						if (mapVotes["current"] < (int)Math.Ceiling((float)total_players / 2)) {
							if (!mapVoteTimer.IsRunning) {
								startNewTimer = true;
							}
						}
					} else if (!mapVoteTimer.IsRunning && total_players > 0) {
						startNewTimer = true;
					}

					if (startNewTimer) {
						mapVoteTimer.Restart();

						byte[] mapVoteMsg = new byte[] { (byte)OpCode.MapVote, 0, 0 };
						
						foreach (Player player in players) {
							if (player != null) {
								server.SendMessageToConnection(player.connection, mapVoteMsg, SendFlags.Reliable);
							}
						}
					}

					if (mapVoteTimer.IsRunning && mapVoteTimer.ElapsedMilliseconds > 30000 && total_players > 0) {
						mapVoteTimer.Stop();

						Tuple<string, int> mostVoted = null;

						foreach (var item in mapVotes) {
							if (!item.Key.Equals("current")) {
								if(mostVoted == null || mostVoted.Item2 < item.Value) {
									mostVoted = Tuple.Create<string, int>(item.Key, item.Value);
								}
							}
						}

						currentMapHash = mostVoted.Item1;

						byte[] newMapMessage = GetCurrentMapHashMessage();

						foreach (Player player in players) {
							if (player != null) {
								server.SendMessageToConnection(player.connection, newMapMessage, SendFlags.Reliable);

								player.currentVote = "current";
							}
						}
					} else if (total_players == 0) {
						mapVoteTimer.Stop();
					}
				}
			}

			Library.Deinitialize();
		}

		// TODO: Move this to utils class
		public static string RemoveMarkup(string msg) {
			string old;

			do {
				old = msg;
				msg = Regex.Replace(msg.Trim(), "</?(?:b|i|color|size|material|quad)[^>]*>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			} while (!msg.Equals(old));

			return msg;
		}

		private void HandleUsername(object data) {
			Tuple<byte[], byte> input = (Tuple<byte[], byte>)data;

			byte[] buffer = input.Item1;
			byte fromID = input.Item2;

			string username = "";
			byte[] IV = new byte[0];
			byte[] usernameBytes;
			byte[] sessionBytes = null;
			string sentFancyName = "";
			string sentSessionID = "";
			if (buffer[1] == 0) {
				usernameBytes = new byte[buffer.Length - 2];

				Array.Copy(buffer, 2, usernameBytes, 0, usernameBytes.Length);

				usernameBytes = ASCIIEncoding.ASCII.GetBytes(RemoveMarkup(ASCIIEncoding.ASCII.GetString(usernameBytes)));
			} else {
				int IVLength = BitConverter.ToInt32(buffer, 2);

				IV = new byte[IVLength];
				usernameBytes = new byte[BitConverter.ToInt32(buffer, 6 + IVLength)];
				int fancyNameLength = BitConverter.ToInt32(buffer, 10 + IVLength + usernameBytes.Length);
				sessionBytes = new byte[BitConverter.ToInt32(buffer, 14 + IVLength + usernameBytes.Length + fancyNameLength)];

				if (IVLength > 0) Array.Copy(buffer, 6, IV, 0, IVLength);
				if (usernameBytes.Length > 0) Array.Copy(buffer, 10 + IVLength, usernameBytes, 0, usernameBytes.Length);
				sentFancyName = ASCIIEncoding.ASCII.GetString(buffer, 14 + IVLength + usernameBytes.Length, fancyNameLength);
				if (sessionBytes.Length > 0) sentSessionID = ASCIIEncoding.ASCII.GetString(buffer, 18 + IVLength + usernameBytes.Length + fancyNameLength, sessionBytes.Length);
			}
			
			var client = new HttpClient();

			string usernameString = "";
			foreach (byte b in usernameBytes) {
				usernameString += b.ToString() + ", ";
			}

			string ivString = "";
			foreach (byte b in IV) {
				ivString += b.ToString() + ", ";
			}

			var values = new Dictionary<string, string> {
				{ "username", usernameString },
				{ "ipaddress", sentSessionID.Equals("") ? players[fromID].ipAddr.GetIP() : sentSessionID },
				{ "iv", ivString } };

			var content = new FormUrlEncodedContent(values);

			HttpResponseMessage response = new HttpResponseMessage();
			try {
				var task = client.PostAsync("http://davisellwood-site.herokuapp.com/api/getreservedname/", content);
				task.Wait();
				response = task.Result;
			} catch(Exception) {
				response.StatusCode = HttpStatusCode.RequestTimeout;
			}
			if (response.StatusCode != HttpStatusCode.OK) {
				// If server cannot be reached use username bytes without markup if standard username buffer and stylized name for encrypted buffer
				if (buffer[1] == 0) {
					username = ASCIIEncoding.ASCII.GetString(usernameBytes);
				} else {
					if(response.StatusCode == HttpStatusCode.BadRequest) {
						username = "Naughty Boy";
					} else {
						username = sentFancyName;
					}
				}
				LogMessageCallback($"Error checking username: {response.StatusCode}", ConsoleColor.White);
			} else {
				var newTask = response.Content.ReadAsStringAsync();
				newTask.Wait();
				username = newTask.Result;
			}

			string[] usernameSplit = username.Split(',');
			if (usernameSplit.Length > 1) {
				bool allInts = true;
				int result = 0;
				foreach (string s in usernameSplit) {
					if (s.Trim() != "" && !Int32.TryParse(s, out result)) {
						allInts = false;
					}
				}
				if (allInts) {
					username = "Naughty Boy";
				}
			}

			LogMessageCallback("Connection {0}'s username is {1}", ConsoleColor.White, fromID, RemoveMarkup(username));

			if (players[fromID] != null) {
				players[fromID].username = username;


				byte[] usernameAdjustmentBytes = new byte[username.Length + 1];
				usernameAdjustmentBytes[0] = (byte)OpCode.UsernameAdjustment;
				Array.Copy(ASCIIEncoding.ASCII.GetBytes(username), 0, usernameAdjustmentBytes, 1, username.Length);
				server.SendMessageToConnection(players[fromID].connection, usernameAdjustmentBytes, SendFlags.Reliable | SendFlags.NoNagle);

				byte[] sendMessage = new byte[username.Length + 2];
				sendMessage[0] = (byte)OpCode.Settings;
				Array.Copy(ASCIIEncoding.ASCII.GetBytes(username), 0, sendMessage, 1, username.Length);
				sendMessage[sendMessage.Length - 1] = fromID;

				players[fromID].usernameMessage = sendMessage;

				foreach (Player player in players) {
					if (player != null && player.playerID != fromID) {
						server.SendMessageToConnection(player.connection, sendMessage, SendFlags.Reliable | SendFlags.NoNagle);
					}
				}
			}
		}

		private async void StartAnnouncing() {
			var client = new HttpClient();
			while (true && API_KEY != "") {
				try {
					int currentPlayers = 0;
					foreach (Player player in players) {
						if (player != null)
							currentPlayers++;
					}

					var values = new Dictionary<string, string> {
					{ "maxPlayers",  players.Length.ToString() },
					{ "serverName", SERVER_NAME },
					{ "currentPlayers", currentPlayers.ToString() },
					{ "serverPort", port.ToString() },
					{ "serverVersion", VERSION_NUMBER },
					{ "apiKey", API_KEY },
					{ "mapName", ENFORCE_MAPS ? mapList[currentMapHash] : "Not enforcing maps" },
					{ "paypal", PAYPAL } };

					var content = new FormUrlEncodedContent(values);

					var response = await client.PostAsync("https://davisellwood-site.herokuapp.com/api/sendserverinfo/", content);
					if (response.StatusCode != HttpStatusCode.OK) {
						LogMessageCallback($"Error announcing: {response.StatusCode}", ConsoleColor.White);
					}
				} catch (Exception e) {
					client = new HttpClient();
				}
				await Task.Delay(10000);
			}
		}
	}
}
