#define VALVESOCKETS_SPAN
#define VALVESOCKETS_INLINING

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
		private static string VERSION_NUMBER = "0.7.2";

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

		[JsonProperty("Max_Upload_Bytes_Per_Second")]
		private static int MAX_UPLOAD = 1073741824;

		[JsonProperty("Max_Queued_Sending_Bytes")]
		private static int MAX_BUFFER = 209715200;

		[JsonProperty("Max_Queued_Bytes_Per_Connection")]
		private static int MAX_BYTES_PENDING = 5242880;

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
											server.SendMessageToConnection(player2.connection, value.Value, SendFlags.Reliable);
										}
										server.FlushMessagesOnConnection(player2.connection);
									}
								}
							}
						}
					}
					break;
				case OpCode.Animation:
					if(players[fromID] != null) {

						bool reliable = buffer[buffer.Length - 1] == (byte)1 ? true : false;
						buffer[buffer.Length - 1] = fromID;

						foreach (Player player in players) {
							if(player != null && player.playerID != fromID) {
								ConnectionStatus status = new ConnectionStatus();
								if (server.GetQuickConnectionStatus(player.connection, ref status)) {
									int bytesPending = status.pendingReliable + status.sentUnackedReliable;

									if (reliable && bytesPending >= MAX_BYTES_PENDING) {
										Console.WriteLine($"Sending animation unreliably to ({player.playerID}) because pending bytes is higher than max");
										reliable = false;
									}
								}

								server.SendMessageToConnection(player.connection, buffer, reliable ? SendFlags.Reliable | SendFlags.NoNagle : SendFlags.Unreliable | SendFlags.NoNagle);
							}
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
							server.SendMessageToConnection(player.connection, sendBuffer, SendFlags.Reliable);
						}
					}
					break;
				case OpCode.MapVote:
					if (ENFORCE_MAPS) {
						string vote = ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 1);

						if(mapList.ContainsKey(vote) || vote.ToLower().Equals("current")) {
							vote = mapList.ContainsKey(vote) && mapList[vote].Equals(currentMapHash) ? "current" : vote;
							players[fromID].currentVote = vote;

							Console.WriteLine("{0} voted for the map {1}", fromID, mapList.ContainsKey(vote) ? mapList[vote] : vote);
						}
					}
					break;
				case OpCode.StillAlive:
					if (players[fromID] != null) server.SendMessageToConnection(players[fromID].connection, buffer, SendFlags.Unreliable | SendFlags.NoNagle);
					break;
			}
		}

		private static void RemovePlayer(uint connection, int ID = -1, bool kicked = false) {
			server.CloseConnection(connection);

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
						server.SendMessageToConnection(player.connection, new byte[] { (byte)OpCode.Disconnect, removedPlayer.playerID }, SendFlags.Reliable);
					}
				}
				players[removedPlayer.playerID] = null;
			}
		}

		public static void ServerLoop() {
			Library.Initialize();

			server = new NetworkingSockets();
			Address address = new Address();
			
			NetworkingUtils utils = new NetworkingUtils();

			utils.SetDebugCallback(DebugType.Debug, (type, message) => {
				Console.WriteLine("Valve Debug - Type: {0}, Message: {1}", type, message);
			});

			unsafe {
				int sendRateMin = 5*1024*1024;
				int sendRateMax = MAX_UPLOAD;
				int sendBufferSize = MAX_BUFFER;
				utils.SetConfigurationValue(ConfigurationValue.SendRateMin, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendRateMin));
				utils.SetConfigurationValue(ConfigurationValue.SendRateMax, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendRateMax));
				utils.SetConfigurationValue(ConfigurationValue.SendBufferSize, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendBufferSize));
			}

			address.SetAddress("::0", port);

			uint listenSocket = server.CreateListenSocket(ref address);
			uint pollGroup = server.CreatePollGroup();

			Console.WriteLine($"Server {SERVER_NAME} started Listening on port {port} for maximum of {MAX_PLAYERS} players\nEnforcing maps is {ENFORCE_MAPS}");

			StartAnnouncing();

			StatusCallback status = (ref StatusInfo info, IntPtr context) => {
				switch (info.connectionInfo.state) {
					case ConnectionState.None:
						break;

					case ConnectionState.Connecting:
						server.AcceptConnection(info.connection);
						server.SetConnectionPollGroup(pollGroup, info.connection);
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
								server.SendMessageToConnection(players[i].connection, versionMessage, SendFlags.Reliable | SendFlags.NoNagle);

								if (ENFORCE_MAPS) {
									server.SendMessageToConnection(players[i].connection, mapListBytes, SendFlags.Reliable);
									server.SendMessageToConnection(players[i].connection, GetCurrentMapHashMessage(), SendFlags.Reliable);
								}

								foreach (Player player in players) {
									if(player != null && player != players[i]) {
										server.SendMessageToConnection(players[i].connection, new byte[] { (byte)OpCode.Connect, player.playerID }, SendFlags.Reliable);
										server.SendMessageToConnection(player.connection, new byte[] { (byte)OpCode.Connect, i }, SendFlags.Reliable);

										if (player.usernameMessage != null) {
											server.SendMessageToConnection(players[i].connection, player.usernameMessage, SendFlags.Reliable);
										}
										if (player.allGearUploaded) {
											foreach (KeyValuePair<string, byte[]> value in player.gear) {
												server.SendMessageToConnection(players[i].connection, value.Value, SendFlags.Reliable);
											}
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
						break;

					case ConnectionState.ClosedByPeer:
						RemovePlayer(info.connection);
						break;
				}
			};

#if VALVESOCKETS_SPAN
			MessageCallback messageCallback = (in NetworkingMessage netMessage) => {
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
			};
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

						//Console.WriteLine("Recieved packet from connection {0}, sending player null: {1}", netMessage.connection, sendingPlayer == null);

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
							Console.WriteLine($"{player.playerID} has been timed out for not responding for 15 seconds");

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
