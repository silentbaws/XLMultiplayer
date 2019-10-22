using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

//TODO: Refactor all of this, it's really bad
//TODO: Refactor the whole mod tbh it's a dumpster fire rn

public enum OpCode : byte {
	Connect = 0,
	Settings = 1,
	Position = 2,
	Animation = 3,
	Texture = 4,
	Chat = 5,
	VersionNumber = 6,
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
	Board = 4
}

public class Player {
	public byte connectionID;
	public string username;

	public bool isGoofy;

	public MultiplayerSkin Pants, Shirt, Hat, Shoes, Board;

	public bool packedAll = false;
	public bool sentAll = false;

	public string IP;

	public Player(byte cID, string user) {
		connectionID = cID;
		username = user;

		Pants = new MultiplayerSkin(MPTextureType.Pants);
		Shirt = new MultiplayerSkin(MPTextureType.Shirt);
		Hat = new MultiplayerSkin(MPTextureType.Hat);
		Shoes = new MultiplayerSkin(MPTextureType.Shoes);
		Board = new MultiplayerSkin(MPTextureType.Board);
	}
}

public class MultiplayerSkin {
	float sizeX, sizeY;
	MPTextureType textureType;

	bool useFull = false;

	public bool finishedCopy = false;

	byte[] buffer;
	int bufferSize = 0;

	public MultiplayerSkin(MPTextureType mpType) {
		textureType = mpType;
	}

	string sep = Path.DirectorySeparatorChar.ToString();

	public string GetTexturePath(byte connectionId) {
		return Directory.GetCurrentDirectory() + sep + "TempClothing" + sep + textureType.ToString() + connectionId.ToString() + ".png";
	}

	public byte[] GetBuffer(byte connectionId) {
		if (this.buffer != null) {
			byte[] sendBuffer = new byte[16 + bufferSize];

			Array.Copy(BitConverter.GetBytes(bufferSize + 12), 0, sendBuffer, 0, 4);

			sendBuffer[4] = (byte)OpCode.Texture;
			sendBuffer[5] = connectionId;
			sendBuffer[6] = (byte)textureType;

			Array.Copy(BitConverter.GetBytes(sizeX), 0, sendBuffer, 7, 4);
			Array.Copy(BitConverter.GetBytes(sizeY), 0, sendBuffer, 11, 4);

			sendBuffer[15] = useFull ? (byte)1 : (byte)0;

			Array.Copy(this.buffer, 0, sendBuffer, 16, bufferSize);

			return sendBuffer;
		} else {
			return null;
		}
	}

	public void SaveTexture(byte connectionId, byte[] recvBuffer) {
		sizeX = BitConverter.ToSingle(recvBuffer, 2);
		sizeY = BitConverter.ToSingle(recvBuffer, 6);

		useFull = recvBuffer[10] == 1 ? true : false;

		this.buffer = new byte[recvBuffer.Length - 11];

		Array.Copy(recvBuffer, 11, this.buffer, 0, this.buffer.Length);

		bufferSize = this.buffer.Length;

		finishedCopy = true;
		Console.WriteLine("Saved texture");
	}
}

public class CurrentMap {
	public static string name = "California Skatepark";
	public static string hash = "1";

	public static byte[] GetMapBytes() {
		byte[] value = new byte[name.Length + hash.Length + 9];
		
		value[0] = (byte)OpCode.MapHash;
		Array.Copy(BitConverter.GetBytes(name.Length), 0, value, 1, 4);
		Array.Copy(ASCIIEncoding.ASCII.GetBytes(name), 0, value, 5, name.Length);
		Array.Copy(BitConverter.GetBytes(hash.Length), 0, value, 5 + name.Length, 4);
		Array.Copy(ASCIIEncoding.ASCII.GetBytes(hash), 0, value, 5 + name.Length + 4, hash.Length);

		return value;
	}
}

public class ServerConfig {
	[JsonProperty("Max_Players")]
	public static int MAX_PLAYERS;
	[JsonProperty("Port")]
	public static int PORT;
	[JsonProperty("Server_Name")]
	public static string SERVER_NAME;
	[JsonProperty("Enforce_Map")]
	public static bool ENFORCE_MAP;
	[JsonProperty("API_Key")]
	public static string API_KEY;
}

public class Server {
	public Socket listener;
	public int port;

	public static Client[] clients;
	public static Player[] players;

	public static UdpClient udpClient;

	const string versionNumber = "0.6.0";

	public static Dictionary<string, string> mapList;

	public static byte[] mapListBytes;

	public class StateObject {
		public Socket workSocket = null;
		public UdpClient udpClient = null;
		public byte[] buffer;
		public int readBytes = 0;
	}

	public class Client {
		public Socket reliableSocket { get; private set; }

		public IPEndPoint remoteEndPoint { get; private set; }
		public IPEndPoint udpRemoteEndPoint;

		public ReceivePacket ReceiveTCP { get; private set; }

		public byte connectionId;
		public bool newConnection = true;

		public long lastAlive = 0;
		public Stopwatch aliveWatch;
		public Stopwatch versionWatch;

		public string currentVote = "current";

		public bool timedOut = false;

		public bool receivedVersion = false;

		public Client(Socket socket, byte connectionId) {
			this.reliableSocket = socket;
			this.connectionId = connectionId;

			this.remoteEndPoint = (IPEndPoint)this.reliableSocket.RemoteEndPoint;

			ReceiveTCP = new ReceivePacket(this.reliableSocket, this, this.connectionId);

			aliveWatch = new Stopwatch();
			aliveWatch.Start();
			lastAlive = aliveWatch.ElapsedMilliseconds;

			ReceiveTCP.StartReceiving();
		}

		public void SendConnectMessage() {
			byte[] buffer = new byte[7];
			Array.Copy(BitConverter.GetBytes(((int)3)), 0, buffer, 0, 4);
			buffer[4] = (byte)OpCode.Connect;
			buffer[5] = 0;
			buffer[6] = connectionId;

			reliableSocket.Send(buffer);
		}

		public void SendDisconnectMessage() {

		}

		public void SendUnreliable(byte[] buffer) {
			if (buffer == null) return;
			try {
				if (udpRemoteEndPoint != null)
					Server.udpClient.Send(buffer, buffer.Length, udpRemoteEndPoint);
			} catch (Exception e) { }
		}

		public void SendReliable(byte[] buffer) {
			if (buffer == null) return;
			try {
				byte[] newBuffer = new byte[buffer.Length + 4];
				Array.Copy(BitConverter.GetBytes(buffer.Length), 0, newBuffer, 0, 4);
				Array.Copy(buffer, 0, newBuffer, 4, buffer.Length);

				reliableSocket.Send(newBuffer);
			} catch (Exception e) { }
		}
	}

	public class ReceivePacket {
		private Socket receiveSocket;
		private Client client;

		bool disconnected = false;

		EndPoint remoteEndPoint;

		public ReceivePacket(Socket socket, Client client, int connectionId) {
			this.receiveSocket = socket;
			this.client = client;
			remoteEndPoint = client.remoteEndPoint;
		}

		public void StartReceiving() {
			try {
				StateObject state = new StateObject();
				state.workSocket = receiveSocket;
				state.buffer = new byte[4];
				state.readBytes = 0;

				receiveSocket.BeginReceiveFrom(state.buffer, 0, state.buffer.Length, SocketFlags.None, ref remoteEndPoint, ReceiveCallback, state);
			} catch (Exception e) { };
		}

		public void ReceiveCallback(IAsyncResult ar) {
			try {
				StateObject state = (StateObject)ar.AsyncState;
				Socket handler = state.workSocket;
				int bytesRead = handler.EndReceive(ar);

				if (bytesRead > 0) {
					state.readBytes += bytesRead;
					Console.WriteLine("read " + state.readBytes.ToString() + " bytes of " + state.buffer.Length.ToString());
					if (state.readBytes < 4) {
						handler.BeginReceiveFrom(state.buffer, state.readBytes, state.buffer.Length - state.readBytes, SocketFlags.None, ref remoteEndPoint, ReceiveCallback, state);
					} else {
						client.lastAlive = client.aliveWatch.ElapsedMilliseconds;
						if (state.readBytes == 4) {
							state.buffer = new byte[BitConverter.ToInt32(state.buffer, 0)];
						}

						if (state.readBytes - 4 == state.buffer.Length) {
							switch ((OpCode)state.buffer[0]) {
								case OpCode.Texture:
									if (!client.receivedVersion) {
										client.SendReliable(new byte[] { (byte)OpCode.Disconnect, 1, client.connectionId });
										client.ReceiveTCP.Disconnect(false);
									}
									switch ((MPTextureType)state.buffer[1]) {
										case MPTextureType.Pants:
											players[client.connectionId].Pants.SaveTexture(client.connectionId, state.buffer);
											break;
										case MPTextureType.Shirt:
											players[client.connectionId].Shirt.SaveTexture(client.connectionId, state.buffer);
											break;
										case MPTextureType.Shoes:
											players[client.connectionId].Shoes.SaveTexture(client.connectionId, state.buffer);
											break;
										case MPTextureType.Board:
											players[client.connectionId].Board.SaveTexture(client.connectionId, state.buffer);
											break;
										case MPTextureType.Hat:
											players[client.connectionId].Hat.SaveTexture(client.connectionId, state.buffer);
											break;
									}
									break;
								case OpCode.Settings:
									players[client.connectionId].isGoofy = state.buffer[1] == 1;
									players[client.connectionId].username = ASCIIEncoding.ASCII.GetString(state.buffer, 2, state.buffer.Length - 2);
									Console.WriteLine("Received username {0} from {1}", players[client.connectionId].username, client.connectionId);
									SendToAllTCP(state.buffer, client.connectionId);
									break;
								case OpCode.VersionNumber:
									string version = ASCIIEncoding.ASCII.GetString(state.buffer, 1, state.buffer.Length - 1);
									if (version != versionNumber) {
										Console.WriteLine("Player " + client.connectionId.ToString() + " tried to connect with an invalid version");

										byte[] disconnectMessage = new byte[versionNumber.Length + 2];
										disconnectMessage[0] = (byte)OpCode.Disconnect;
										disconnectMessage[disconnectMessage.Length - 1] = client.connectionId;
										Array.Copy(ASCIIEncoding.ASCII.GetBytes(versionNumber), 0, disconnectMessage, 1, versionNumber.Length);

										client.SendReliable(disconnectMessage);

										client.ReceiveTCP.Disconnect(false);
									} else {
										SendToAllTCP(new byte[] { (byte)OpCode.Connect }, client.connectionId);
										client.SendConnectMessage();
										client.receivedVersion = true;

										foreach (Client c in clients) {
											if (c != null && c.connectionId != client.connectionId) {
												Console.WriteLine("Sending {0} connect to new connection {1}", c.connectionId, client.connectionId);
												byte[] buffer = new byte[2];
												buffer[0] = (byte)OpCode.Connect;
												buffer[1] = c.connectionId;

												client.SendReliable(buffer);

												byte[] username = ASCIIEncoding.ASCII.GetBytes(players[c.connectionId].username);
												buffer = new byte[username.Length + 3];
												buffer[0] = (byte)OpCode.Settings;
												buffer[1] = players[c.connectionId].isGoofy ? (byte)1 : (byte)0;
												Array.Copy(username, 0, buffer, 2, username.Length);
												buffer[buffer.Length - 1] = c.connectionId;

												client.SendReliable(buffer);
											}
										}

										if (ServerConfig.ENFORCE_MAP) {
											client.SendReliable(CurrentMap.GetMapBytes());
											client.SendReliable(Server.mapListBytes);
										}
									}
									break;
								case OpCode.Chat:
									Console.WriteLine("Chat Message");
									if (state.buffer.Length != 1)
										SendToAllTCP(state.buffer, client.connectionId);
									break;
								case OpCode.MapVote:
									string mapHash = ASCIIEncoding.ASCII.GetString(state.buffer, 1, state.buffer.Length - 1);
									client.currentVote = mapHash;
									break;
							}

							StartReceiving();
						} else {
							handler.BeginReceiveFrom(state.buffer, state.readBytes - 4, state.buffer.Length - state.readBytes + 4, SocketFlags.None, ref remoteEndPoint, ReceiveCallback, state);
						}
					}
				} else {
					Disconnect(client.timedOut);
				}
			} catch (Exception e) {
				if (receiveSocket.Connected) {
					StartReceiving();
				} else {
					Console.WriteLine(e.ToString());
					Disconnect(client.timedOut);
				}
			}
		}

		public void Disconnect(bool timeout) {
			if (receiveSocket != null && !disconnected) {
				try {
					receiveSocket.Shutdown(SocketShutdown.Both);
				} catch (Exception e) { }
				try {
					receiveSocket.Close();
				} catch (Exception e) { }
				SendToAllTCP(new byte[] { (byte)OpCode.Disconnect }, client.connectionId);
				players[client.connectionId] = null;
				clients[client.connectionId] = null;
				disconnected = true;
				Console.WriteLine("Disconnect from {0} on game server {1}", client.connectionId, timeout ? "connection timed out" : "");
			}
		}
	}

	public Server(int port) {
		for (int i = 0; i < clients.Length; i++) {
			clients[i] = null;
		}
		listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		this.port = port;
		StartListening();
		StartAnnouncing();

		udpClient = new UdpClient();
		udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, ServerConfig.PORT));
		udpClient.DontFragment = true;
		BeginReceivingUDP();
	}

	public static byte[] GetMapList() {
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

	private static void ChangeMap(string hash) {
		if (hash != "current") {
			CurrentMap.hash = hash;
			CurrentMap.name = mapList[hash];
		}

		foreach (Client client in clients) {
			if (client != null) {
				client.currentVote = "current";
				if(hash != "current") client.SendReliable(CurrentMap.GetMapBytes());
			}
		}
	}

	public void StartListening() {
		try {
			Console.WriteLine("Started listening on game server");
			listener.Bind(new IPEndPoint(IPAddress.Any, port));
			listener.Listen(ServerConfig.MAX_PLAYERS);

			listener.BeginAccept(AcceptCallback, listener);
		} catch (Exception e) {
			Console.WriteLine(e.ToString());
		}
	}

	public async void StartAnnouncing() {
		var client = new HttpClient();
		while (true && ServerConfig.API_KEY != "") {
			try {
				int currentPlayers = 0;
				foreach(Client c in clients) {
					if (c != null)
						currentPlayers++;
				}

				var values = new Dictionary<string, string> {
					{ "maxPlayers",  players.Length.ToString() },
					{ "serverName", ServerConfig.SERVER_NAME },
					{ "currentPlayers", currentPlayers.ToString() },
					{ "serverPort", ServerConfig.PORT.ToString() },
					{ "serverVersion", versionNumber },
					{ "apiKey", ServerConfig.API_KEY },
					{ "mapName", CurrentMap.name }
				};

				Console.WriteLine(currentPlayers.ToString());

				var content = new FormUrlEncodedContent(values);

				var response = await client.PostAsync("http://www.davisellwood.com/api/sendserverinfo/", content);
				if (response.StatusCode != HttpStatusCode.OK) {
					Console.WriteLine($"Error announcing: {response.StatusCode}");
				}
			} catch (Exception e) {
				client = new HttpClient();
			}
			await Task.Delay(10000);
		}
	}

	public void AcceptCallback(IAsyncResult ar) {
		try {
			Console.WriteLine("Began connection on game server");
			Socket tempSocket = (Socket)ar.AsyncState;
			Socket acceptedSocket = tempSocket.EndAccept(ar);
			bool foundConnection = false;
			byte connectionId = 0;

			for (int i = 0; i < clients.Length; i++) {
				if (clients[i] == null) {
					connectionId = (byte)i;
					foundConnection = true;
					break;
				}
			}

			if (foundConnection) {
				clients[connectionId] = new Client(acceptedSocket, connectionId);
				players[connectionId] = new Player(connectionId, "");

				clients[connectionId].versionWatch = new Stopwatch();
				clients[connectionId].versionWatch.Start();

				//SendToAllTCP(new byte[] { (byte)OpCode.Connect }, connectionId);
			} else {
				int connections = 0;
				foreach (Client client in clients) {
					if (client != null) {
						connections++;
					}
				}
				Console.WriteLine("Refused connection due to no open slots, currently {0} connections of {1} filled", connections, ServerConfig.MAX_PLAYERS);
			}

			listener.BeginAccept(AcceptCallback, listener);
		} catch (Exception e) {
			Console.WriteLine(e.ToString());
			listener.BeginAccept(AcceptCallback, listener);
		}
	}

	public static void SendToAllUDP(byte[] buffer, byte fromId) {
		byte[] newBuffer = new byte[buffer.Length + 1];
		Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
		newBuffer[buffer.Length] = fromId;

		foreach (Client client in clients) {
			if (client != null && client.connectionId != fromId) {
				client.SendUnreliable(newBuffer);
			}
		}
	}

	public static void SendToAllTCP(byte[] buffer, byte fromId) {
		byte[] newBuffer = new byte[buffer.Length + 1];
		Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
		newBuffer[buffer.Length] = fromId;

		foreach (Client client in clients) {
			if (client != null && client.connectionId != fromId)
				client.SendReliable(newBuffer);
		}
	}

	private void BeginReceivingUDP() {
		try {
			udpClient.BeginReceive(ReceiveCallbackUDP, udpClient);
		} catch (Exception e) { };
	}

	public void ReceiveCallbackUDP(IAsyncResult ar) {
		try {
			UdpClient tempUDP = (UdpClient)ar.AsyncState;
			IPEndPoint tempEndPoint = new IPEndPoint(IPAddress.Any, ServerConfig.PORT);

			byte[] buffer = tempUDP.EndReceive(ar, ref tempEndPoint);

			foreach (Client client in clients) {
				if (client != null) {
					if (tempEndPoint != null && ((client.udpRemoteEndPoint == null && client.remoteEndPoint != null && tempEndPoint.Address.Equals(client.remoteEndPoint.Address)) || client.udpRemoteEndPoint.Equals(tempEndPoint))) {
						switch ((OpCode)buffer[0]) {
							case OpCode.StillAlive:
								udpClient.Send(buffer, buffer.Length, tempEndPoint);
								client.lastAlive = client.aliveWatch.ElapsedMilliseconds;
								if (client.udpRemoteEndPoint == null) {
									client.udpRemoteEndPoint = tempEndPoint;
								}
								break;
							case OpCode.Position:
								SendToAllUDP(buffer, client.connectionId);
								break;
							case OpCode.Animation:
								SendToAllUDP(buffer, client.connectionId);
								break;
						}
						break;
					}
				}
			}

			BeginReceivingUDP();
		} catch (Exception e) {
			Console.WriteLine(e.ToString());
			BeginReceivingUDP();
		}
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

	private static bool votingInitialized = false;
	private static Stopwatch votingTimer = new Stopwatch();

	public static int Main(String[] args) {
		string sep = Path.DirectorySeparatorChar.ToString();
		JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar.ToString() + "ServerConfig.json"));

		Console.WriteLine("Creating game server on port {0}, with a maximum of {1} players. Enforcing maps is {2}", ServerConfig.PORT, ServerConfig.MAX_PLAYERS, ServerConfig.ENFORCE_MAP);

		clients = new Client[ServerConfig.MAX_PLAYERS];
		players = new Player[ServerConfig.MAX_PLAYERS];

		if (ServerConfig.ENFORCE_MAP) {
			Server.mapList = new Dictionary<string, string>();

			mapList.Add("0", "Courthouse");
			mapList.Add("1", "California Skatepark");

			if(Directory.Exists(Directory.GetCurrentDirectory() + sep + "Maps")) {
				string[] files = Directory.GetFiles(Directory.GetCurrentDirectory() + sep + "Maps");
				if(files.Length < 1) {
					Console.WriteLine("\nWARNING\nWARNING: FAILED TO FIND ANY CUSTOM MAPS IN THE MAPS DIRECTORY ONLY COURTHOUSE AND CALIFORNIA WILL BE USED\nWARNING\n");
				} else {
					Console.WriteLine("Begin hashing maps\n");
					foreach (string file in files) {
						string hash = CalculateMD5(file);
						try {
							Server.mapList.Add(hash, Path.GetFileName(file));
							Console.WriteLine("Adding map: " + Path.GetFileName(file) + ", with hash: " + hash + " to servers map list\n");
						} catch (ArgumentException) {
							Console.WriteLine("**WARNING** MAP " + Path.GetFileName(file) + " HASH OVERLAPS WITH " + Server.mapList[hash] + " PLEASE DM ME TO LET ME KNOW\n");
						}
					}
					Console.WriteLine("Finished hashing maps");
				}
				mapListBytes = Server.GetMapList();
			} else {
				Console.WriteLine("Failed to find Directory " + Directory.GetCurrentDirectory() + sep + "Maps");
				Console.Write("Press any key to close the server...");
				Console.In.Read();
				return 0;
			}
		} else {
			CurrentMap.name = "No set map";
			CurrentMap.hash = "-1";
		}

		Server server = new Server(ServerConfig.PORT);
		while (true) {
			int currentPlayers = 0;
			int votesForMapChange = 0;
			if (!votingInitialized) {
				foreach(Client client in clients) {
					if(client != null) {
						currentPlayers++;
						if(!client.currentVote.Equals("current", StringComparison.CurrentCultureIgnoreCase)) {
							votesForMapChange++;
						}
					}
				}

				if(currentPlayers == 0) {
					CurrentMap.name = "California skatepark";
					CurrentMap.hash = "1";
				}

				if(votesForMapChange > Math.Floor(currentPlayers / 2f)) {
					votingInitialized = true;
					votingTimer.Restart();
					SendToAllTCP(new byte[] { (byte)OpCode.MapVote }, 255);
				}
			} else {
				if(votingTimer.ElapsedMilliseconds > 30000) {
					votingInitialized = false;
					List<Tuple<string, int>> votes = new List<Tuple<string, int>>();
					foreach(Client client in clients) {
						if(client != null) {
							int index = votes.FindIndex(vote => vote.Item1.Equals(client.currentVote));
							if(index != -1) {
								votes[index] = Tuple.Create(votes[index].Item1, votes[index].Item2 + 1);
							} else {
								if (client.currentVote != "current") votes.Add(Tuple.Create(client.currentVote, 1));
							}
						}
					}

					int topVoteIndex = 0;
					for (int i = 1; i < votes.Count; i++) {
						if(votes[i].Item2 > votes[topVoteIndex].Item2) {
							topVoteIndex = i;
						}
					}

					ChangeMap(votes[topVoteIndex].Item1);
				}
			}

			foreach (Client client in clients) {
				if (client != null && (client.aliveWatch.ElapsedMilliseconds - client.lastAlive > 10000 || (client.versionWatch != null && client.versionWatch.ElapsedMilliseconds > 5000 && !client.receivedVersion))) {
					client.timedOut = true;
					client.ReceiveTCP.Disconnect(client.timedOut);
				}
			}

			foreach (Client client in clients) {
				if (client != null && client.newConnection && client.reliableSocket.Connected) {
					foreach (Player player in players) {
						if (player != null && player.connectionID != client.connectionId && player.packedAll && player != null && client.reliableSocket != null) {
							Console.WriteLine("SENDING TEXTURES");
							try {
								client.reliableSocket.Send(player.Pants.GetBuffer(player.connectionID));
								client.reliableSocket.Send(player.Shirt.GetBuffer(player.connectionID));
								client.reliableSocket.Send(player.Shoes.GetBuffer(player.connectionID));
								client.reliableSocket.Send(player.Board.GetBuffer(player.connectionID));
								client.reliableSocket.Send(player.Hat.GetBuffer(player.connectionID));
							} catch (Exception e) { }
						}
						client.newConnection = false;
					}
				}
			}

			foreach (Player player in players) {
				if (player != null) {
					if (!player.packedAll) {
						if (player.Hat.finishedCopy && player.Board.finishedCopy && player.Shoes.finishedCopy && player.Shirt.finishedCopy && player.Pants.finishedCopy) {
							player.packedAll = true;
							Console.WriteLine("Packed all for player {0}", player.connectionID);
						}
					}
					if (player.packedAll && !player.sentAll) {
						foreach (Client client in clients) {
							if (client != null && client.connectionId != player.connectionID && player != null && client.reliableSocket != null) {
								Console.WriteLine("SENDING TEXTURES");
								try {
									client.reliableSocket.Send(player.Pants.GetBuffer(player.connectionID));
									client.reliableSocket.Send(player.Shirt.GetBuffer(player.connectionID));
									client.reliableSocket.Send(player.Shoes.GetBuffer(player.connectionID));
									client.reliableSocket.Send(player.Board.GetBuffer(player.connectionID));
									client.reliableSocket.Send(player.Hat.GetBuffer(player.connectionID));
								} catch (Exception e) { }
							}
						}
						player.sentAll = true;
					}
				}
			}
		}
		return 0;
	}
}
