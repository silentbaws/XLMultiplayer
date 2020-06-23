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

		public byte[] usernameMessage = null;

		public bool allGearUploaded = false;
		public Dictionary<string, byte[]> gear = new Dictionary<string, byte[]>();

		public string currentVote = "current";

		public Stopwatch timeoutWatch = new Stopwatch();

		public List<string> previousMessages = new List<string>();

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
				if (!gear.ContainsKey(((MPTextureType)i).ToString())) {
					allGearUploaded = false;
				}
			}
		}

		public string GetIPAddress() {
			return ipAddr.GetIP();
		}
	}
}
