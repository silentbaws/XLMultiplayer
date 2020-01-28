using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Valve.Sockets;

namespace XLMultiplayerServer {
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

	class Player {
		public byte playerID;
		public string username;
		public uint connection;
		public Address ipAddr;

		public Player(byte pID, uint conn, Address addr) {
			this.playerID = pID;
			this.connection = conn;
			this.ipAddr = addr;
		}
	}

	class Server {
		public static bool RUNNING = true;
		public static ushort port = 7777;

		private static byte MAX_PLAYERS = 10;

		private static Player[] players = new Player[MAX_PLAYERS];

		public static int Main(String[] args) {
			var serverTask = Task.Run(() => ServerLoop());
			Task.Run(() => CommandLoop());

			serverTask.Wait();
			return 0;
		}


		public static void CommandLoop() {
			while (RUNNING) {
				string input = Console.ReadLine();

				if(input.Equals("QUIT", StringComparison.CurrentCultureIgnoreCase)) {
					RUNNING = false;
				}

			}
		}

		private static void ProcessMessage(byte[] buffer, byte fromID, NetworkingSockets server) {
			switch ((OpCode)buffer[0]) {
				case OpCode.Animation:
					bool reliable = buffer[buffer.Length - 1] == (byte)1 ? true : false;
					buffer[buffer.Length - 1] = fromID;

					foreach(Player player in players) {
						if(player != null && player.playerID != fromID) {
							server.SendMessageToConnection(player.connection, buffer, reliable ? SendType.Reliable : SendType.Unreliable);
						}
					}
					break;
			}
		}

		public static void ServerLoop() {
			Library.Initialize();

			NetworkingSockets server = new NetworkingSockets();
			Address address = new Address();

			address.SetAddress("::0", port);

			uint listenSocket = server.CreateListenSocket(ref address);

			StatusCallback status = (info, context) => {
				switch (info.connectionInfo.state) {
					case ConnectionState.None:
						break;

					case ConnectionState.Connecting:
						server.AcceptConnection(info.connection);
						break;

					case ConnectionState.Connected:
						Console.WriteLine("Client connected - ID: " + info.connection + ", IP: " + info.connectionInfo.address.GetIP());

						bool openSlot = false;

						for(byte i = 0; i < MAX_PLAYERS; i++) {
							if(players[i] == null) {
								players[i] = new Player(i, info.connection, info.connectionInfo.address);

								foreach(Player player in players) {
									if(player != null && player != players[i]) {
										server.SendMessageToConnection(players[i].connection, new byte[] { (byte)OpCode.Connect, player.playerID }, SendType.Reliable);
										server.SendMessageToConnection(player.connection, new byte[] { (byte)OpCode.Connect, i }, SendType.Reliable);
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
						Console.WriteLine("Client disconnected - ID: " + info.connection + ", IP: " + info.connectionInfo.address.GetIP());

						Player removedPlayer = null;
						foreach(Player player in players) {
							if(player != null && player.connection == info.connection) {
								removedPlayer = player;
								break;
							}
						}

						if(removedPlayer != null) {
							foreach(Player player in players) {
								if(player != null && player != removedPlayer) {
									server.SendMessageToConnection(player.connection, new byte[] { (byte)OpCode.Disconnect, removedPlayer.playerID }, SendType.Reliable);
								}
							}
						}

						players[removedPlayer.playerID] = null;
						server.CloseConnection(info.connection);
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

						Console.WriteLine("Recieved packet from connection {0}, sending player null: {1}", netMessage.connection, sendingPlayer == null);

						if (sendingPlayer != null)
							ProcessMessage(messageData, sendingPlayer.playerID, server);

						netMessage.Destroy();
					}
				}
			}

			Library.Deinitialize();
		}
	}
}
