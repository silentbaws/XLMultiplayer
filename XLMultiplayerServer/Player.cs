using System;
using System.Collections.Generic;
using System.Diagnostics;
using Valve.Sockets;

namespace XLMultiplayerServer {
	public class Player {
		public byte playerID;
		public string username;
		public uint connection;
		public uint fileConnection;
		public Address ipAddr;

		public PluginPlayer pluginPlayer = null;

		public byte[] usernameMessage = null;

		List<byte> gearStream = new List<byte>();
		public bool completedGearStream = false;

		public string currentVote = "current";

		public Stopwatch timeoutWatch = new Stopwatch();

		public List<string> previousMessages = new List<string>();
		public List<byte> loadedPlugins = new List<byte>();

		public Player(byte pID, uint conn, Address addr) {
			this.playerID = pID;
			this.connection = conn;
			this.ipAddr = addr;
		}

		public bool AddGear(byte[] buffer) {
			ushort currentMessage = BitConverter.ToUInt16(buffer, 1);
			ushort totalMessages = BitConverter.ToUInt16(buffer, 3);

			byte[] bytesToAdd = new byte[buffer.Length - 5];

			Array.Copy(buffer, 5, bytesToAdd, 0, bytesToAdd.Length);

			gearStream.AddRange(bytesToAdd);

			if (currentMessage == totalMessages) {
				gearStream.Add(playerID);
				completedGearStream = true;
				return true;
			}

			return false;
		}

		public void SendGear(uint connectionToSend, NetworkingSockets server) {
			byte[] sendData = new byte[gearStream.Count + 6];

			if (sendData.Length > Library.maxMessageSize) {
				ushort totalMessages = (ushort)Math.Ceiling((double)sendData.Length / Library.maxMessageSize);

				byte[] textureDataArray = gearStream.ToArray();

				for (ushort currentMessage = 0; currentMessage < totalMessages; currentMessage++) {
					int startIndex = currentMessage == 0 ? 0 : currentMessage * (Library.maxMessageSize - 6);

					byte[] messagePart = new byte[Math.Min(Library.maxMessageSize, textureDataArray.Length - (Library.maxMessageSize - 6) * currentMessage + 6)];
					messagePart[0] = (byte)OpCode.Texture;
					Array.Copy(BitConverter.GetBytes(currentMessage), 0, messagePart, 1, 2);
					Array.Copy(BitConverter.GetBytes(totalMessages - 1), 0, messagePart, 3, 2);
					Array.Copy(textureDataArray, startIndex, messagePart, 5, messagePart.Length - 6);
					messagePart[messagePart.Length - 1] = playerID;

					server.SendMessageToConnection(connectionToSend, messagePart, SendFlags.Reliable);
				}
			} else {
				sendData[0] = (byte)OpCode.Texture;
				Array.Copy(BitConverter.GetBytes((ushort)0), 0, sendData, 1, 2);
				Array.Copy(BitConverter.GetBytes((ushort)0), 0, sendData, 3, 2);
				Array.Copy(gearStream.ToArray(), 0, sendData, 5, gearStream.Count);
				sendData[sendData.Length - 1] = playerID;

				server.SendMessageToConnection(connectionToSend, sendData, SendFlags.Reliable);
			}

		}

		public string GetIPAddress() {
			return ipAddr.GetIP();
		}
	}
}
