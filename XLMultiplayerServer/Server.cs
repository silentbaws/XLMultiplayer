using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Valve.Sockets;

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

	class Player {
		public byte playerID;
		public string username;
		public uint connection;
		public Address ipAddr;

		public byte[] usernameMessage = null;

		public bool allGearUploaded = false;
		public Dictionary<string, byte[]> gear = new Dictionary<string, byte[]>();

		public string currentVote = "current";

		public Stopwatch timeoutWatch = new Stopwatch();

		public Player(byte pID, uint conn, Address addr) {
			this.playerID = pID;
			this.connection = conn;
			this.ipAddr = addr;
		}

		public void AddGear(byte[] buffer) {
			byte[] newBuffer = new byte[buffer.Length + 1];
			Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
			newBuffer[buffer.Length] = playerID;
			gear.Add(((MPTextureType)buffer[1]).ToString(), newBuffer);

			allGearUploaded = true;
			for (byte i = 0; i < 10; i++) {
				if (!gear.ContainsKey(((MPTextureType)i).ToString())){
					allGearUploaded = false;
				}
			}
		}
	}

	class Server {
		// TODO: Update version number with versions
		private static string VERSION_NUMBER = "0.7.0";

		private static NetworkingSockets server;

		public static bool RUNNING = true;

		[JsonProperty("Server_Name")]
		private static string SERVER_NAME = "";

		[JsonProperty("Port")]
		private static ushort port = 7777;

		[JsonProperty("Max_Players")]
		private static byte MAX_PLAYERS = 10;

		[JsonProperty("Enforce_Map")]
		private static bool ENFORCE_MAPS = true;

		[JsonProperty("API_Key")]
		private static string API_KEY = "";

		[JsonProperty("Maps_Folder")]
		private static string mapsDir = "";

		[JsonProperty("Paypal_Link")]
		private static string PAYPAL = "";

		private static Player[] players = new Player[MAX_PLAYERS];
		private static int total_players = 0;

		private static string sep = Path.DirectorySeparatorChar.ToString();
		
		private static byte[] mapListBytes = null;
		private static Dictionary<string, string> mapList = new Dictionary<string, string>();
		private static Dictionary<string, int> mapVotes = new Dictionary<string, int>();

		private static Stopwatch mapVoteTimer = new Stopwatch();

		private static string currentMapHash = "1";

		public static int Main(String[] args) {
			Console.ForegroundColor = ConsoleColor.White;
			Console.BackgroundColor = ConsoleColor.Black;

			Console.WriteLine("Starting server initialization");

			if (File.Exists(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar.ToString() + "ServerConfig.json")) {
				JsonConvert.DeserializeObject<Server>(File.ReadAllText(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar.ToString() + "ServerConfig.json"));
				players = new Player[MAX_PLAYERS];
			} else {
				Console.WriteLine("Could not find server config file");
				Console.In.Read();
				return 0;
			}

			if (ENFORCE_MAPS) {
				mapListBytes = GenerateMapList();

				if (mapListBytes == null) {
					return 0;
				}
			}

			var serverTask = Task.Run(() => ServerLoop());
			Task.Run(() => CommandLoop());

			Console.WriteLine("Server initialization finished");

			serverTask.Wait();
			return 0;
		}

		static string CalculateMD5(string filename) {
			using (var md5 = MD5.Create()) {
				using (var stream = File.OpenRead(filename)) {
					byte[] hash = null;
					long size = new System.IO.FileInfo(filename).Length;
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

		private static byte[] GenerateMapList() {
			mapList.Clear();

			mapList.Add("0", "Courthouse");
			mapList.Add("1", "California Skatepark");

			if (mapsDir == "") {
				mapsDir = Directory.GetCurrentDirectory() + sep + "Maps";
				Console.WriteLine($"No maps folder set so using {mapsDir}");
			}

			if (Directory.Exists(mapsDir)) {
				string[] files = Directory.GetFiles(mapsDir);
				if (files.Length < 1) {
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("\nWARNING: FAILED TO FIND ANY CUSTOM MAPS IN THE MAPS DIRECTORY ONLY COURTHOUSE AND CALIFORNIA WILL BE USED\n");
					Console.ForegroundColor = ConsoleColor.White;
				} else {
					Console.WriteLine("Begin hashing maps\n");
					foreach (string file in files) {
						string hash = CalculateMD5(file);
						try {
							mapList.Add(hash, Path.GetFileName(file));
							Console.WriteLine("Adding map: " + Path.GetFileName(file) + ", with hash: " + hash + " to servers map list\n");
						} catch (ArgumentException) {
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine("**WARNING** MAP " + Path.GetFileName(file) + " HASH OVERLAPS WITH " + mapList[hash] + " PLEASE DM ME TO LET ME KNOW\n");
							Console.ForegroundColor = ConsoleColor.White;
						}
					}
					Console.WriteLine("Finished hashing maps");
				}
			} else {
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("\nWARNING: FAILED TO FIND MAPS DIRECTORY \"{0}\" SO ONLY COURTHOUSE AND CALIFORNIA WILL BE USED\n", mapsDir);
				Console.ForegroundColor = ConsoleColor.White;
			}

			byte[] mapListBytes = GetMapListBytes();
			return mapListBytes;
		}

		private static byte[] GetMapListBytes() {
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

		private static byte[] GetCurrentMapHashMessage() {
			byte[] mapHashBytes = ASCIIEncoding.ASCII.GetBytes(currentMapHash);
			byte[] mapNameBytes = ASCIIEncoding.ASCII.GetBytes(mapList[currentMapHash]);

			byte[] newMapMessage = new byte[mapHashBytes.Length + mapNameBytes.Length + 5];

			newMapMessage[0] = (byte)OpCode.MapHash;
			Array.Copy(BitConverter.GetBytes(mapNameBytes.Length), 0, newMapMessage, 1, 4);
			Array.Copy(mapNameBytes, 0, newMapMessage, 5, mapNameBytes.Length);
			Array.Copy(mapHashBytes, 0, newMapMessage, 5 + mapNameBytes.Length, mapHashBytes.Length);

			return newMapMessage;
		}

		public static void CommandLoop() {
			while (RUNNING) {
				string input = Console.ReadLine();

				if(input.Equals("QUIT", StringComparison.CurrentCultureIgnoreCase)) {
					RUNNING = false;
				}else if (input.ToLower().StartsWith("kick")) {
					string kickIDString = input.ToLower().Replace("kick ", "");
					int kickID = -1;
					if (Int32.TryParse(kickIDString, out kickID)) {
						if (players[kickID] != null) {
							Console.WriteLine("Kicking player {0}", kickID);
							RemovePlayer(players[kickID].connection, kickID, true);
						}
					} else {
						Console.WriteLine("Invalid player ID");
					}
				}
			}
		}

		private static void ProcessMessage(byte[] buffer, byte fromID, NetworkingSockets server) {
			if(!Enum.IsDefined(typeof(OpCode), (OpCode)buffer[0]) || players[fromID] == null) {
				return;
			}

			players[fromID].timeoutWatch.Restart();

			switch ((OpCode)buffer[0]) {
				case OpCode.Settings:
					Thread usernameThread = new Thread(new ParameterizedThreadStart(Server.HandleUsername));
					usernameThread.IsBackground = true;
					usernameThread.Start(Tuple.Create(buffer, fromID));
					break;
				case OpCode.Texture: {
						Console.WriteLine("Received Texture from " + fromID);
						Player player = players[fromID];
						if (player != null && player.playerID == fromID) {
							player.AddGear(buffer);
							if (player.allGearUploaded) {
								foreach (Player player2 in players) {
									if (player2 != null && player2.playerID != fromID) {
										foreach (KeyValuePair<string, byte[]> value in player.gear) {
											server.SendMessageToConnection(player2.connection, value.Value, SendType.Reliable | SendType.NoNagle);
										}
									}
								}
							}
						}
					}
					break;
				case OpCode.Animation:
					bool reliable = buffer[buffer.Length - 1] == (byte)1 ? true : false;
					buffer[buffer.Length - 1] = fromID;

					foreach(Player player in players) {
						if(player != null && player.playerID != fromID) {
							server.SendMessageToConnection(player.connection, buffer, reliable ? SendType.Reliable : SendType.Unreliable);
						}
					}
					break;
				case OpCode.Chat:
					string contents = ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 1);
					Console.WriteLine("Chat Message from {0} saying: {1}", fromID, contents);

					byte[] sendBuffer = new byte[buffer.Length + 1];
					Array.Copy(buffer, 0, sendBuffer, 0, buffer.Length);
					sendBuffer[buffer.Length] = fromID;

					foreach (Player player in players) {
						if(player != null) {
							server.SendMessageToConnection(player.connection, sendBuffer, SendType.Reliable);
						}
					}
					break;
				case OpCode.MapVote:
					if (ENFORCE_MAPS) {
						string vote = ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 1);

						if(mapList.ContainsKey(vote) || vote.ToLower().Equals("current")) {
							vote = mapList[vote].Equals(currentMapHash) ? "current" : vote;
							players[fromID].currentVote = vote;

							Console.WriteLine("{0} voted for the map {1}", fromID, mapList[vote]);
						}
					}
					break;
				case OpCode.StillAlive:
					if (players[fromID] != null) server.SendMessageToConnection(players[fromID].connection, buffer, SendType.Unreliable);
					break;
			}
		}

		private static void RemovePlayer(uint connection, int ID = -1, bool kicked = false) {
			Player removedPlayer = null;
			if (ID == -1) {
				foreach (Player player in players) {
					if (player != null && player.connection == connection) {
						removedPlayer = player;
						break;
					}
				}
			} else {
				removedPlayer = players[ID];
			}

			if (removedPlayer != null) {
				if (!kicked) Console.WriteLine("Client disconnected - ID: " + removedPlayer.playerID);
				foreach (Player player in players) {
					if (player != null && player != removedPlayer) {
						server.SendMessageToConnection(player.connection, new byte[] { (byte)OpCode.Disconnect, removedPlayer.playerID }, SendType.Reliable);
					}
				}
				players[removedPlayer.playerID] = null;
			}

			server.CloseConnection(connection);
		}

		public static void ServerLoop() {
			Library.Initialize();

			server = new NetworkingSockets();
			Address address = new Address();
			
			NetworkingUtils utils = new NetworkingUtils();

			utils.SetDebugCallback(DebugType.Important, (type, message) => {
				Console.WriteLine("Valve Debug - Type: {0}, Message: {1}", type, message);
			});

			unsafe {
				int sendRateMin = 1024*1024*10;
				int sendRateMax = 2147483647;
				int sendBufferSize = 209715200;
				utils.SetConfiguratioValue(ConfigurationValue.SendRateMin, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendRateMin));
				utils.SetConfiguratioValue(ConfigurationValue.SendRateMax, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendRateMax));
				utils.SetConfiguratioValue(ConfigurationValue.SendBufferSize, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendBufferSize));
			}

			address.SetAddress("::0", port);

			uint listenSocket = server.CreateListenSocket(ref address);

			Console.WriteLine($"Server {SERVER_NAME} started Listening on port {port} for maximum of {MAX_PLAYERS} players\nEnforcing maps is {ENFORCE_MAPS}");

			StartAnnouncing();

			StatusCallback status = (info, context) => {
				switch (info.connectionInfo.state) {
					case ConnectionState.None:
						break;

					case ConnectionState.Connecting:
						server.AcceptConnection(info.connection);
						break;

					case ConnectionState.Connected:
						Console.WriteLine("Client connected - IP: " + info.connectionInfo.address.GetIP());

						bool openSlot = false;

						for(byte i = 0; i < MAX_PLAYERS; i++) {
							if(players[i] == null) {
								players[i] = new Player(i, info.connection, info.connectionInfo.address);

								byte[] versionNumber = ASCIIEncoding.ASCII.GetBytes(VERSION_NUMBER);
								byte[] versionMessage = new byte[versionNumber.Length + 1];

								versionMessage[0] = (byte)OpCode.VersionNumber;
								Array.Copy(versionNumber, 0, versionMessage, 1, versionNumber.Length);
								server.SendMessageToConnection(players[i].connection, versionMessage, SendType.Reliable);

								if (ENFORCE_MAPS) {
									server.SendMessageToConnection(players[i].connection, mapListBytes, SendType.Reliable | SendType.NoNagle);
									server.SendMessageToConnection(players[i].connection, GetCurrentMapHashMessage(), SendType.Reliable);
								}

								foreach (Player player in players) {
									if(player != null && player != players[i]) {
										server.SendMessageToConnection(players[i].connection, new byte[] { (byte)OpCode.Connect, player.playerID }, SendType.Reliable);
										server.SendMessageToConnection(player.connection, new byte[] { (byte)OpCode.Connect, i }, SendType.Reliable);

										if (player.usernameMessage != null) {
											server.SendMessageToConnection(players[i].connection, player.usernameMessage, SendType.Reliable | SendType.NoNagle);
										}
										if (player.allGearUploaded) {
											foreach (KeyValuePair<string, byte[]> value in player.gear) {
												server.SendMessageToConnection(players[i].connection, value.Value, SendType.Reliable | SendType.NoNagle);
											}
										}
									}
								}

								openSlot = true;
								break;
							}
						}

						if (!openSlot) {
							server.CloseConnection(info.connection);
						}
						break;

					case ConnectionState.ClosedByPeer:
						RemovePlayer(info.connection);
						break;
				}
			};
			const int maxMessages = 256;

			NetworkingMessage[] netMessages = new NetworkingMessage[maxMessages];

			while (RUNNING) {
				server.DispatchCallback(status);

				int netMessagesCount = server.ReceiveMessagesOnListenSocket(listenSocket, netMessages, maxMessages);

				if (netMessagesCount > 0) {
					for (int i = 0; i < netMessagesCount; i++) {
						ref NetworkingMessage netMessage = ref netMessages[i];

						byte[] messageData = new byte[netMessage.length];
						netMessage.CopyTo(messageData);

						Player sendingPlayer = null;
						foreach(Player player in players) {
							if(player != null && player.connection == netMessage.connection) {
								sendingPlayer = player;
								break;
							}
						}

						//Console.WriteLine("Recieved packet from connection {0}, sending player null: {1}", netMessage.connection, sendingPlayer == null);

						if (sendingPlayer != null)
							ProcessMessage(messageData, sendingPlayer.playerID, server);

						netMessage.Destroy();
					}
				}
				
				mapVotes.Clear();
				total_players = 0;
				foreach (Player player in players) {
					if (player != null) {
						total_players++;

						if (player.timeoutWatch.ElapsedMilliseconds > 10000) {
							Console.WriteLine($"{player.playerID} has been timed out for not responding for 10 seconds");

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
								server.SendMessageToConnection(player.connection, mapVoteMsg, SendType.Reliable);
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
								server.SendMessageToConnection(player.connection, newMapMessage, SendType.Reliable);

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

		private static string RemoveMarkup(string msg) {
			string old;

			do {
				old = msg;
				msg = Regex.Replace(msg.Trim(), "</?(?:b|i|color|size|material|quad)[^>]*>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			} while (!msg.Equals(old));

			return msg;
		}

		public static void HandleUsername(object data) {
			Tuple<byte[], byte> input = (Tuple<byte[], byte>)data;

			byte[] buffer = input.Item1;
			byte fromID = input.Item2;

			string username = "";
			byte[] IV = new byte[0];
			byte[] usernameBytes;
			string sentFancyName = "";
			if (buffer[1] == 0) {
				usernameBytes = new byte[buffer.Length - 2];

				Array.Copy(buffer, 2, usernameBytes, 0, usernameBytes.Length);

				usernameBytes = ASCIIEncoding.ASCII.GetBytes(RemoveMarkup(ASCIIEncoding.ASCII.GetString(usernameBytes)));
			} else {
				int IVLength = BitConverter.ToInt32(buffer, 2);

				IV = new byte[IVLength];
				usernameBytes = new byte[BitConverter.ToInt32(buffer, 6 + IVLength)];

				if (IVLength > 0) Array.Copy(buffer, 6, IV, 0, IVLength);
				if (usernameBytes.Length > 0) Array.Copy(buffer, 10 + IVLength, usernameBytes, 0, usernameBytes.Length);
				sentFancyName = ASCIIEncoding.ASCII.GetString(buffer, 10 + IVLength + usernameBytes.Length, buffer.Length - IVLength - usernameBytes.Length - 10);
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
				{ "ipaddress", players[fromID].ipAddr.GetIP() },
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
				Console.WriteLine($"Error checking username: {response.StatusCode}");
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

			Console.WriteLine("Connection {0}'s username is {1}", fromID, username);

			if (players[fromID] != null) {
				players[fromID].username = username;


				byte[] usernameAdjustmentBytes = new byte[username.Length + 1];
				usernameAdjustmentBytes[0] = (byte)OpCode.UsernameAdjustment;
				Array.Copy(ASCIIEncoding.ASCII.GetBytes(username), 0, usernameAdjustmentBytes, 1, username.Length);
				server.SendMessageToConnection(players[fromID].connection, usernameAdjustmentBytes, SendType.Reliable);

				byte[] sendMessage = new byte[username.Length + 2];
				sendMessage[0] = (byte)OpCode.Settings;
				Array.Copy(ASCIIEncoding.ASCII.GetBytes(username), 0, sendMessage, 1, username.Length);
				sendMessage[sendMessage.Length - 1] = fromID;

				players[fromID].usernameMessage = sendMessage;

				foreach (Player player in players) {
					if (player != null && player.playerID != fromID) {
						server.SendMessageToConnection(player.connection, sendMessage, SendType.Reliable);
					}
				}
			}
		}

		private static async void StartAnnouncing() {
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
					{ "paypal", PAYPAL} };

					var content = new FormUrlEncodedContent(values);

					var response = await client.PostAsync("https://davisellwood-site.herokuapp.com/api/sendserverinfo/", content);
					if (response.StatusCode != HttpStatusCode.OK) {
						Console.WriteLine($"Error announcing: {response.StatusCode}");
					}
				} catch (Exception e) {
					client = new HttpClient();
				}
				await Task.Delay(10000);
			}
		}
	}
}
