#define VALVESOCKETS_SPAN

using System;
using System.Collections.Generic;
using Valve.Sockets;

namespace XLMultiplayerServer {
	class FileServer {
		public static NetworkingSockets server;
		public static uint pollGroup;
		public static uint listenSocket;

		public static void StatusCallbackFunction(ref StatusInfo info, IntPtr context) {
				switch (info.connectionInfo.state) {
					case ConnectionState.None:
						break;

					case ConnectionState.Connecting: {
						if (info.connectionInfo.listenSocket != listenSocket) {
							Server.StatusCallbackFunction(ref info, context);
							break;
						}

						Console.WriteLine("connecting on file server");

						if (Server.bannedIPs.Contains(info.connectionInfo.address.GetIP())) {
							Console.WriteLine("Ban player attempted to connect to the server, IP: {0}", info.connectionInfo.address.GetIP());
							server.CloseConnection(info.connection);
						} else {
							server.AcceptConnection(info.connection);
							server.SetConnectionPollGroup(pollGroup, info.connection);
						}
					} break;

					case ConnectionState.Connected: {
						if (info.connectionInfo.listenSocket != listenSocket) {
							Server.StatusCallbackFunction(ref info, context);
							break;
						}

						if (Server.motdBytes != null) server.SendMessageToConnection(info.connection, Server.motdBytes);

						Console.WriteLine("connected on file server");
					} break;

					case ConnectionState.ClosedByPeer:
					case ConnectionState.ProblemDetectedLocally:
						Server.RemovePlayer(info.connection);
					break;
				}
			}

		public static void ServerLoop() {
			Library.Initialize();

			server = new NetworkingSockets();
			Address address = new Address();

			Console.WriteLine($"Gameplay port: {Server.port}, File Server Port: {(ushort)(Server.port + 1)}");

			address.SetAddress("::0", (ushort)(Server.port + 1));

			listenSocket = server.CreateListenSocket(ref address);
			pollGroup = server.CreatePollGroup();

			NetworkingUtils utils = new NetworkingUtils();

			unsafe {
				int sendRateMin = 512000;
				int sendRateMax = Server.FILE_MAX_UPLOAD;
				utils.SetConfigurationValue(ConfigurationValue.SendRateMin, ConfigurationScope.ListenSocket, new IntPtr(listenSocket), ConfigurationDataType.Int32, new IntPtr(&sendRateMin));
				utils.SetConfigurationValue(ConfigurationValue.SendRateMax, ConfigurationScope.ListenSocket, new IntPtr(listenSocket), ConfigurationDataType.Int32, new IntPtr(&sendRateMax));
			}
			StatusCallback status = StatusCallbackFunction;
			
			MessageCallback messageCallback = (in NetworkingMessage netMessage) => {
				byte[] message = new byte[netMessage.length];
				netMessage.CopyTo(message);

				if ((OpCode)message[0] == OpCode.Connect) {
					uint originalConnection = BitConverter.ToUInt32(message, 1);

					Player newPlayer = null;
					foreach (Player player in Server.players) {
						if (player != null && player.connection == originalConnection) {
							player.fileConnection = netMessage.connection;
							newPlayer = player;
							break;
						}
					}

					if (newPlayer == null) {
						Console.WriteLine("Connection on file server doesn't exist on gameplay server");

						server.CloseConnection(netMessage.connection);
					} else {
						foreach (Player player in Server.players) {
							if (player != null) {
								foreach (KeyValuePair<string, byte[]> value in player.gear) {
									server.SendMessageToConnection(newPlayer.fileConnection, value.Value, SendFlags.Reliable);
								}
							}
						}
						server.FlushMessagesOnConnection(newPlayer.fileConnection);
					}
				} else {
					foreach (Player player in Server.players) {
						if (player != null && player.fileConnection == netMessage.connection) {
							Server.ProcessMessage(message, player.playerID, Server.server);
						}
					}
				}
			};
			while (Server.RUNNING) {
				server.DispatchCallback(status);

				server.ReceiveMessagesOnPollGroup(pollGroup, messageCallback, 256);
			}
		}
	}
}
