using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public enum OpCode : byte {
	Connect = 0,
	Settings = 1,
	Position = 2,
	Animation = 3,
	Texture = 4,
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
	public int connectionID;
	public string username;

	public bool isGoofy;

	public MultiplayerSkin Pants, Shirt, Hat, Shoes, Board;

	public bool packedAll = false;
	public bool sentAll = false;

	public string IP;

	public Player(int cID, string user) {
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

	public bool finishedCopy = false;

	int bufferSize = 0;

	public MultiplayerSkin(MPTextureType mpType) {
		textureType = mpType;
	}

	string sep = Path.DirectorySeparatorChar.ToString();

	public string GetTexturePath(int connectionId) {
		return Directory.GetCurrentDirectory() + sep + "TempClothing" + sep + textureType.ToString() + connectionId.ToString() + ".png";
	}

	public byte[] GetBuffer(int connectionId) {
		if (File.Exists(GetTexturePath(connectionId))) {
			byte[] file = File.ReadAllBytes(GetTexturePath(connectionId));
			byte[] buffer = new byte[15 + file.Length];
			Array.Copy(BitConverter.GetBytes(bufferSize + 11), 0, buffer, 0, 4);
			buffer[4] = (byte)OpCode.Texture;
			buffer[5] = (byte)connectionId;
			buffer[6] = (byte)textureType;
			Array.Copy(BitConverter.GetBytes(sizeX), 0, buffer, 7, 4);
			Array.Copy(BitConverter.GetBytes(sizeY), 0, buffer, 11, 4);
			Array.Copy(file, 0, buffer, 15, file.Length);

			return buffer;
		}
		else {
			return null;
		}
	}

	public void SaveTexture(int connectionId, byte[] buffer) {
		Console.WriteLine("Save texture");
		sizeX = BitConverter.ToSingle(buffer, 2);
		sizeY = BitConverter.ToSingle(buffer, 6);
		byte[] file = new byte[buffer.Length - 10];
		Array.Copy(buffer, 10, file, 0, file.Length);

		bufferSize = file.Length;

		if (!Directory.Exists(Directory.GetCurrentDirectory() + sep + "TempClothing"))
			Directory.CreateDirectory(Directory.GetCurrentDirectory() + sep + "TempClothing");

		File.WriteAllBytes(Directory.GetCurrentDirectory() + sep + "TempClothing" + sep + textureType.ToString() + connectionId.ToString() + ".png", file);
		finishedCopy = true;
		Console.WriteLine("Saved texture");
	}

	public void DeleteTexture(int connectionId) {
		if (File.Exists(Directory.GetCurrentDirectory() + sep + "TempClothing" + sep + textureType.ToString() + connectionId.ToString() + ".png"))
			File.Delete(Directory.GetCurrentDirectory() + sep + "TempClothing" + sep + textureType.ToString() + connectionId.ToString() + ".png");
	}
}


public class ServerConfig {
	[JsonProperty("Max_Players")]
	public static int MAX_PLAYERS;
	[JsonProperty("Port")]
	public static int PORT;
}

public class Server {
	public Socket listener;
	public int port;

	public static Client[] clients;
	public static Player[] players;

	public static UdpClient udpClient;

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

		public int connectionId;
		public bool newConnection = true;

		public long lastAlive = 0;
		public Stopwatch aliveWatch;

		public bool timedOut = false;

		public Client(Socket socket, int connectionId) {
			this.reliableSocket = socket;
			this.connectionId = connectionId;

			this.remoteEndPoint = (IPEndPoint)this.reliableSocket.RemoteEndPoint;

			ReceiveTCP = new ReceivePacket(this.reliableSocket, this, this.connectionId);

			aliveWatch = new Stopwatch();
			aliveWatch.Start();
			lastAlive = aliveWatch.ElapsedMilliseconds;

			SendConnectMessage();

			foreach (Client client in clients) {
				if (client != null && client.connectionId != connectionId) {
					Console.WriteLine("Sending {0} connect to new connection {1}", client.connectionId, connectionId);
					byte[] buffer = new byte[5];
					buffer[0] = (byte)OpCode.Connect;
					Array.Copy(BitConverter.GetBytes(client.connectionId), 0, buffer, 1, 4);

					this.SendReliable(buffer);

					byte[] username = ASCIIEncoding.ASCII.GetBytes(players[client.connectionId].username);
					buffer = new byte[username.Length + 6];
					buffer[0] = (byte)OpCode.Settings;
					buffer[1] = players[client.connectionId].isGoofy ? (byte)1 : (byte)0;
					Array.Copy(username, 0, buffer, 2, username.Length);
					Array.Copy(BitConverter.GetBytes(client.connectionId), 0, buffer, buffer.Length - 4, 4);

					this.SendReliable(buffer);
				}
			}

			ReceiveTCP.StartReceiving();
		}

		public void SendConnectMessage() {
			byte[] buffer = new byte[10];
			Array.Copy(BitConverter.GetBytes(((int)6)), 0, buffer, 0, 4);
			buffer[4] = (byte)OpCode.Connect;
			buffer[5] = 0;
			Array.Copy(BitConverter.GetBytes(connectionId), 0, buffer, 6, 4);

			reliableSocket.Send(buffer);
		}

		public void SendDisconnectMessage() {

		}

		public void SendUnreliable(byte[] buffer) {
			if (buffer == null) return;
			try {
				if (udpRemoteEndPoint != null)
					Server.udpClient.Send(buffer, buffer.Length, udpRemoteEndPoint);
			}
			catch (Exception e) { }
		}

		public void SendReliable(byte[] buffer) {
			if (buffer == null) return;
			try {
				byte[] newBuffer = new byte[buffer.Length + 4];
				Array.Copy(BitConverter.GetBytes(buffer.Length), 0, newBuffer, 0, 4);
				Array.Copy(buffer, 0, newBuffer, 4, buffer.Length);

				reliableSocket.Send(newBuffer);
			}
			catch (Exception e) { }
		}
	}

	public class ReceivePacket {
		private Socket receiveSocket;
		private Client client;

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
			}
			catch (Exception e) { };
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
					}
					else {
						client.lastAlive = client.aliveWatch.ElapsedMilliseconds;
						if (state.readBytes == 4) {
							state.buffer = new byte[BitConverter.ToInt32(state.buffer, 0)];
						}

						if (state.readBytes - 4 == state.buffer.Length) {
							switch ((OpCode)state.buffer[0]) {
								case OpCode.Texture:
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
							}

							StartReceiving();
						}
						else {
							handler.BeginReceiveFrom(state.buffer, state.readBytes - 4, state.buffer.Length - state.readBytes + 4, SocketFlags.None, ref remoteEndPoint, ReceiveCallback, state);
						}
					}
				}
				else {
					Disconnect(client.timedOut);
				}
			}
			catch (Exception e) {
				if (receiveSocket.Connected) {
					StartReceiving();
				}
				else {
					Console.WriteLine(e.ToString());
					Disconnect(client.timedOut);
				}
			}
		}

		public void Disconnect(bool timeout) {
			try {
				if (receiveSocket != null) {
					receiveSocket.Shutdown(SocketShutdown.Both);
					receiveSocket.Close();
					SendToAllTCP(new byte[] { (byte)OpCode.Disconnect }, client.connectionId);
					players[client.connectionId].Pants.DeleteTexture(client.connectionId);
					players[client.connectionId].Shirt.DeleteTexture(client.connectionId);
					players[client.connectionId].Shoes.DeleteTexture(client.connectionId);
					players[client.connectionId].Board.DeleteTexture(client.connectionId);
					players[client.connectionId].Hat.DeleteTexture(client.connectionId);
					players[client.connectionId] = null;
					clients[client.connectionId] = null;
					Console.WriteLine("Disconnect from {0} on game server {1}", client.connectionId, timeout ? "connection timed out" : "");
				}
			}
			catch (Exception e) {
				if (client != null) {
					if (players[client.connectionId] != null) {
						players[client.connectionId].Pants.DeleteTexture(client.connectionId);
						players[client.connectionId].Shirt.DeleteTexture(client.connectionId);
						players[client.connectionId].Shoes.DeleteTexture(client.connectionId);
						players[client.connectionId].Board.DeleteTexture(client.connectionId);
						players[client.connectionId].Hat.DeleteTexture(client.connectionId);
						players[client.connectionId] = null;
					}
					clients[client.connectionId] = null;
					players[client.connectionId] = null;
				}
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

	public void StartListening() {
		try {
			Console.WriteLine("Started listening on game server");
			listener.Bind(new IPEndPoint(IPAddress.Any, port));
			listener.Listen(ServerConfig.MAX_PLAYERS);

			listener.BeginAccept(AcceptCallback, listener);
		}
		catch (Exception e) {
			Console.WriteLine(e.ToString());
		}
	}

	public async void StartAnnouncing() {
		var client = new HttpClient();
		while (true) {
			string data = $"{{\"n_players\":{players.Length}, \"map\":\"map_name\"}}";
			string myJson = $"{{\"port\":{port}, \"data\":{data}}}";
			var response = await client.PostAsync(
				 "http://sxl-server-announcer.herokuapp.com/v2",
				 new StringContent(myJson, Encoding.UTF8, "application/json"));
			if (response.StatusCode != HttpStatusCode.OK) {
				Console.WriteLine($"Error announcing: error {response.StatusCode}");
			}
			await Task.Delay(5000);
		}
	}

	public void AcceptCallback(IAsyncResult ar) {
		try {
			Console.WriteLine("Began connection on game server");
			Socket tempSocket = (Socket)ar.AsyncState;
			Socket acceptedSocket = tempSocket.EndAccept(ar);
			int connectionId = -1;

			for (int i = 0; i < clients.Length; i++) {
				if (clients[i] == null) {
					connectionId = i;
					break;
				}
			}

			if (connectionId != -1) {
				clients[connectionId] = new Client(acceptedSocket, connectionId);
				players[connectionId] = new Player(connectionId, "");

				SendToAllTCP(new byte[] { (byte)OpCode.Connect }, connectionId);
			}
			else {
				int connections = 0;
				foreach (Client client in clients) {
					if (client != null) {
						connections++;
					}
				}
				Console.WriteLine("Refused connection due to no open slots, currently {0} connections of {1} filled", connections, ServerConfig.MAX_PLAYERS);
			}

			listener.BeginAccept(AcceptCallback, listener);
		}
		catch (Exception e) {
			Console.WriteLine(e.ToString());
			listener.BeginAccept(AcceptCallback, listener);
		}
	}

	public static void SendToAllUDP(byte[] buffer, int fromId) {
		byte[] newBuffer = new byte[buffer.Length + 4];
		Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
		Array.Copy(BitConverter.GetBytes(fromId), 0, newBuffer, buffer.Length, 4);

		foreach (Client client in clients) {
			if (client != null && client.connectionId != fromId) {
				client.SendUnreliable(newBuffer);
			}
		}
	}

	public static void SendToAllTCP(byte[] buffer, int fromId) {
		byte[] newBuffer = new byte[buffer.Length + 4];
		Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
		Array.Copy(BitConverter.GetBytes(fromId), 0, newBuffer, buffer.Length, 4);

		foreach (Client client in clients) {
			if (client != null && client.connectionId != fromId)
				client.SendReliable(newBuffer);
		}
	}

	private void BeginReceivingUDP() {
		try {
			udpClient.BeginReceive(ReceiveCallbackUDP, udpClient);
		}
		catch (Exception e) { };
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
		}
		catch (Exception e) {
			Console.WriteLine(e.ToString());
			BeginReceivingUDP();
		}
	}

	public static int Main(String[] args) {
		JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar.ToString() + "ServerConfig.json"));

		Console.WriteLine("Creating game server on port {0}, with a maximum of {1} players", ServerConfig.PORT, ServerConfig.MAX_PLAYERS);

		clients = new Client[ServerConfig.MAX_PLAYERS];
		players = new Player[ServerConfig.MAX_PLAYERS];

		Server server = new Server(ServerConfig.PORT);
		while (true) {

			foreach (Client client in clients) {
				if (client != null && client.aliveWatch.ElapsedMilliseconds - client.lastAlive > 10000) {
					client.timedOut = true;
					client.ReceiveTCP.Disconnect(client.timedOut);
				}
			}

			int i = 0;
			//foreach(Client client in clients) {
			//	if (client != null) {
			//		if (client.reliableSocket == null || client.aliveWatch == null || client.aliveWatch.ElapsedMilliseconds - client.lastAlive > 5000 || client.timedOut || client.ReceiveTCP == null) {
			//			if (client.ReceiveTCP != null && client.reliableSocket != null && client.reliableSocket.Connected) {
			//				Console.WriteLine("Disconnecting cunt {0}", i);
			//				client.reliableSocket.Disconnect(false);
			//			} else {
			//				Console.WriteLine("Setting player {0} to null", i);
			//				if (client.reliableSocket != null) {
			//					try { client.reliableSocket.Close(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
			//					clients[i] = null;
			//					players[i] = null;
			//					break;
			//				}
			//			}
			//		}
			//	}
			//	i++;
			//}

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
							}
							catch (Exception e) { }
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
								}
								catch (Exception e) { }
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
