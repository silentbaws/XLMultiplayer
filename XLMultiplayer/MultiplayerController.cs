using ReplayEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Valve.Sockets;

// TODO: Redo the multiplayer texture system
//			-> Send paths for non-custom gear
//			-> Send hashes of full size textures for custom gear along with compressed texture
//			-> Only send hashes/paths from server unless client requests texture data

// TODO: Send alive thread, read ping and packet loss

namespace XLMultiplayer {
	public enum OpCode : byte {
		Connect = 0,
		Settings = 1,
		VersionNumber = 2,
		Animation = 3,
		Texture = 4,
		Chat = 5,
		MapHash = 7,
		MapVote = 8,
		MapList = 9,
		StillAlive = 254,
		Disconnect = 255
	}

	class MultiplayerController : MonoBehaviour{
		// Valve Sockets stuff
		private NetworkingSockets client = null;
		private StatusCallback status = null;
		private uint connection;
		private const int maxMessages = 256;
		private NetworkingMessage[] netMessages;
		DebugCallback debugCallbackDelegate = null;

		public List<string> chatMessages = new List<string>();

		public bool isConnected { get; private set; } = false;

		private StreamWriter debugWriter;

		private MultiplayerLocalPlayerController playerController;
		private List<MultiplayerRemotePlayerController> remoteControllers = new List<MultiplayerRemotePlayerController>();

		private bool replayStarted = false;

		private int sentAnimUpdates = 0;
		private float previousSentAnimationTime = -1;

		private int tickRate = 30;

		private bool sendingUpdates = false;
		private float timeSinceLastUpdate = 0.0f;

		// Open replay editor on start to prevent null references to replay editor instance
		public void Start() {
			if (ReplayEditorController.Instance == null) {
				GameManagement.GameStateMachine.Instance.ReplayObject.SetActive(true);
				StartCoroutine(TurnOffReplay());
			}
		}

		// Turn off replay editor as soon as it's instance is not null
		private IEnumerator TurnOffReplay() {
			while (ReplayEditorController.Instance == null)
				yield return new WaitForEndOfFrame();

			GameManagement.GameStateMachine.Instance.ReplayObject.SetActive(false);
			yield break;
		}

		public void ConnectToServer(string ip, ushort port, string user) {
			// Create a debug log file
			int i = 0;
			while (this.debugWriter == null) {
				string filename = "Multiplayer Debug Client" + (i == 0 ? "" : " " + i.ToString()) + ".txt";
				try {
					this.debugWriter = new StreamWriter(filename);
				} catch (Exception) {
					this.debugWriter = null;
					i++;
				}
			}
			this.debugWriter.AutoFlush = true;
			this.debugWriter.WriteLine("Attempting to connect to server ip {0} on port {1}", ip, port.ToString());

			MultiplayerUtils.serverMapDictionary.Clear();

			this.playerController = new MultiplayerLocalPlayerController(this.debugWriter);
			this.playerController.ConstructPlayer();
			this.playerController.username = user;

			Library.Initialize();

			client = new NetworkingSockets();

			IPAddress serverIP = null;
			try {
				IPHostEntry hostInfo = Dns.GetHostEntry(ip);
				foreach (IPAddress address in hostInfo.AddressList) {
					if (address.MapToIPv4() != null) {
						serverIP = address.MapToIPv4();
					}
				}
			} catch (Exception) { }
			if (serverIP == null) {
				if (!IPAddress.TryParse(ip, out serverIP)) {
					DisconnectFromServer();
					return;
				}
			}

			Address remoteAddress = new Address();
			remoteAddress.SetAddress(serverIP.ToString(), port);

			connection = client.Connect(ref remoteAddress);

			status = StatusCallback;

			netMessages = new NetworkingMessage[maxMessages];
		}

		private void StatusCallback(StatusInfo info, IntPtr context) {
			switch (info.connectionInfo.state) {
				case ConnectionState.None:
					break;

				case ConnectionState.Connected:
					ConnectionCallback();
					break;

				case ConnectionState.ClosedByPeer:
					//Client disconnected from server
					this.debugWriter.WriteLine("Disconnected from server");
					Main.utilityMenu.SendImportantChat("<b><color=\"red\">You have been disconnected from the server</color></b>", 7500);
					DisconnectFromServer();
					break;

				case ConnectionState.ProblemDetectedLocally:
					//Client unable to connect
					this.debugWriter.WriteLine("Unable to connect to server");
					Main.utilityMenu.SendImportantChat("<b><color=\"red\">You could not connect to the server</color></b>", 7500);
					DisconnectFromServer();
					break;
			}
		}

		// Called when player connects to server
		private void ConnectionCallback() {
			this.debugWriter.WriteLine("Successfully connected to server");
			isConnected = true;

			debugCallbackDelegate = (type, message) => {
				if (this.debugWriter != null)
					this.debugWriter.WriteLine("Valve Debug - Type: {0}, Message: {1}", type, message);
			};

			NetworkingUtils utils = new NetworkingUtils();

			utils.SetDebugCallback(DebugType.Important, debugCallbackDelegate);

			Main.utilityMenu.chat = "";
			Main.utilityMenu.previousMessageCount = 0;

			#if DEBUG
			unsafe {
				// From data I saw a typical example of udp packets over the internet would be 0.3% loss, 25% reorder
				// For testing I'll use 25% reorder, 30ms delay on reorder, 150ms ping, and 0.2% loss

				// Re-order 25% of packets and add 30ms delay on reordered packets
				//float reorderPercent = 25f;
				//int reorderTime = 30;
				//utils.SetConfiguratioValue(ConfigurationValue.FakePacketReorderSend, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Float, new IntPtr(&reorderPercent));
				//utils.SetConfiguratioValue(ConfigurationValue.FakePacketReorderTime, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&reorderTime));

				//// Fake 150ms ping
				//int pingTime = 150;
				//utils.SetConfiguratioValue(ConfigurationValue.FakePacketLagSend, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&pingTime));

				//// Simulate 0.2% packet loss
				//float lossPercent = 0.2f;
				//utils.SetConfiguratioValue(ConfigurationValue.FakePacketLossSend, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Float, new IntPtr(&lossPercent));
			}
			#endif
		}

		public void StartLoadMap(string path) {
			this.StartCoroutine(LoadMap(path));
		}

		public IEnumerator LoadMap(string path) {
			while (!sendingUpdates) {
				yield return new WaitForEndOfFrame();
			}
			//Load map with path
			LevelSelectionController levelSelectionController = GameManagement.GameStateMachine.Instance.LevelSelectionObject.GetComponentInChildren<LevelSelectionController>();
			GameManagement.GameStateMachine.Instance.LevelSelectionObject.SetActive(true);
			LevelInfo target = levelSelectionController.Levels.Find(level => level.path.Equals(path));
			if (target == null) {
				target = levelSelectionController.CustomLevels.Find(level => level.path.Equals(path));
			}
			levelSelectionController.StartCoroutine(levelSelectionController.LoadLevel(target));
			StartCoroutine(CloseAfterLoad());
			yield break;
		}

		private IEnumerator CloseAfterLoad() {
			while (GameManagement.GameStateMachine.Instance.IsLoading) {
				yield return new WaitForEndOfFrame();
			}
			GameManagement.GameStateMachine.Instance.LevelSelectionObject.SetActive(false);
			Main.menu.CloseMultiplayerMenu();
			yield break;
		}

		public void Update() {
			if (client == null) return;

			if (GameManagement.GameStateMachine.Instance.CurrentState.GetType() != typeof(GameManagement.ReplayState)) {
				if (replayStarted) {
					foreach (MultiplayerRemotePlayerController controller in remoteControllers) {
						controller.EndReplay();
					}
				}
				replayStarted = false;
			} else {
				if (!replayStarted) {
					replayStarted = true;

					foreach (MultiplayerRemotePlayerController controller in remoteControllers) {
						controller.PrepareReplay();
					}
				}
			}

			// Don't allow use of map selection
			if (GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.LevelSelectionState) && MultiplayerUtils.serverMapDictionary.Count > 0 && isConnected) {
				GameManagement.GameStateMachine.Instance.RequestPlayState();
			}

			if (sendingUpdates) {
				timeSinceLastUpdate += Time.unscaledDeltaTime;
				if (timeSinceLastUpdate > 1f / (float)tickRate) {
					SendUpdate();
					timeSinceLastUpdate = 0.0f;
				}
			}

			UpdateClient();

			// Lerp frames using frame buffer
			foreach (MultiplayerRemotePlayerController controller in this.remoteControllers) {
				if (controller != null) {
					controller.ApplyTextures();

					if (GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.ReplayState)) {
						controller.replayController.TimeScale = ReplayEditorController.Instance.playbackController.TimeScale;
						controller.replayController.SetPlaybackTime(ReplayEditorController.Instance.playbackController.CurrentTime);
					}
					controller.LerpNextFrame(GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.ReplayState));
				}
			}
		}

		private void UpdateClient() {
			client.DispatchCallback(status);

			if(debugCallbackDelegate != null)
				GC.KeepAlive(debugCallbackDelegate);

			int netMessagesCount = client.ReceiveMessagesOnConnection(connection, netMessages, maxMessages);

			if (netMessagesCount > 0) {
				for (int i = 0; i < netMessagesCount; i++) {
					ref NetworkingMessage netMessage = ref netMessages[i];

					byte[] messageData = new byte[netMessage.length];
					netMessage.CopyTo(messageData);

					ProcessMessage(messageData);

					netMessage.Destroy();
				}
			}
		}

		private void ProcessMessage(byte[] buffer) {
			OpCode opCode = (OpCode)buffer[0];
			byte playerID = buffer[buffer.Length - 1];

			byte[] newBuffer = new byte[buffer.Length - 2];

			if(newBuffer.Length != 0) {
				Array.Copy(buffer, 1, newBuffer, 0, buffer.Length - 2);
			}

			if (opCode != OpCode.Animation) this.debugWriter.WriteLine("Recieved message with opcode {0}, and length {1}", opCode, buffer.Length);

			switch (opCode) {
				case OpCode.Connect:
					AddPlayer(playerID);
					break;
				case OpCode.Disconnect:
					// TODO: Delay destruction and removal until after they're no longer in replay
					MultiplayerRemotePlayerController player = remoteControllers.Find(c => c.playerID == playerID);
					chatMessages.Add("Player <color=\"yellow\">" + player.username + "{" + player.playerID + "}</color> <b><color=\"red\">DISCONNECTED</color></b>");
					RemovePlayer(playerID);
					break;
				case OpCode.VersionNumber:
					string serverVersion = ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 1);

					if (serverVersion.Equals(Main.modEntry.Version.ToString())) {
						this.debugWriter.WriteLine("Server version matches client version start encoding");

						byte[] usernameBytes = Encoding.ASCII.GetBytes(this.playerController.username);
						SendBytes(OpCode.Settings, usernameBytes, true);

						this.StartCoroutine(this.playerController.EncodeTextures());

						sendingUpdates = true;
					} else {
						this.debugWriter.WriteLine("server version {0} does not match client version {1}", serverVersion, Main.modEntry.Version.ToString());
						DisconnectFromServer();
					}
					break;
				case OpCode.Settings:
					MultiplayerRemotePlayerController remotePlayer = remoteControllers.Find((p) => { return p.playerID == playerID; });
					if (remotePlayer != null) {
						remotePlayer.username = RemoveMarkup(ASCIIEncoding.ASCII.GetString(newBuffer));
						chatMessages.Add("Player <color=\"yellow\">" + remotePlayer.username + "{" + remotePlayer.playerID + "}</color> <b><color=\"green\">CONNECTED</color></b>");
					}
					break;
				case OpCode.Texture:
					MultiplayerRemotePlayerController remoteOwner = remoteControllers.Find((p) => { return p.playerID == playerID; });
					if (remoteOwner == null) {
						this.debugWriter.WriteLine("Texture owner not found");
					}
					switch ((MPTextureType)newBuffer[0]) {
						case MPTextureType.Shirt:
							remoteOwner.shirtMPTex.SaveTexture(playerID, newBuffer);
							break;
						case MPTextureType.Pants:
							remoteOwner.pantsMPTex.SaveTexture(playerID, newBuffer);
							break;
						case MPTextureType.Shoes:
							remoteOwner.shoesMPTex.SaveTexture(playerID, newBuffer);
							break;
						case MPTextureType.Hat:
							remoteOwner.hatMPTex.SaveTexture(playerID, newBuffer);
							break;
						case MPTextureType.Deck:
							remoteOwner.deckMPTex.SaveTexture(playerID, newBuffer);
							break;
						case MPTextureType.Grip:
							remoteOwner.gripMPTex.SaveTexture(playerID, newBuffer);
							break;
						case MPTextureType.Trucks:
							remoteOwner.truckMPTex.SaveTexture(playerID, newBuffer);
							break;
						case MPTextureType.Wheels:
							remoteOwner.wheelMPTex.SaveTexture(playerID, newBuffer);
							break;
						case MPTextureType.Body:
							remoteOwner.bodyMPTex.SaveTexture(playerID, newBuffer);
							break;
						case MPTextureType.Head:
							remoteOwner.headMPTex.SaveTexture(playerID, newBuffer);
							break;
					}
					break;
				case OpCode.Animation:
					byte[] packetData = new byte[newBuffer.Length - 4];
					Array.Copy(newBuffer, 4, packetData, 0, packetData.Length);

					byte[] decompressedData = Decompress(packetData);

					byte[] animationData = new byte[decompressedData.Length + 4];
					Array.Copy(newBuffer, 0, animationData, 0, 4);
					Array.Copy(decompressedData, 0, animationData, 4, decompressedData.Length);

					this.remoteControllers.Find(p => p.playerID == playerID).UnpackAnimations(animationData);
					break;
				case OpCode.Chat:
					MultiplayerRemotePlayerController remoteSender = this.remoteControllers.Find(p => p.playerID == playerID);
					string cleanedMessage = RemoveMarkup(ASCIIEncoding.ASCII.GetString(newBuffer));

					if (remoteSender != null) {
						cleanedMessage = "<b>" + remoteSender.username + "</b>{" + playerID + "}: " + cleanedMessage;
						chatMessages.Add(cleanedMessage);
					}
					break;
				case OpCode.MapList:
					MultiplayerUtils.LoadServerMaps(buffer);
					break;
				case OpCode.MapHash:
					string mapName = MultiplayerUtils.ChangeMap(buffer);

					if(mapName != null) {
						Main.utilityMenu.DisplayMessage("MAP NOT FOUND - DISCONNECTED FROM SERVER", "There is no map matching the servers map \"" + mapName + "\" if you have this map it may be a different version, check to see if there's been a new version", 15000);

						DisconnectFromServer();
					}

					break;
				case OpCode.MapVote:
					Main.utilityMenu.SendImportantChat("<b><color=#ff00ffff>MAP VOTING HAS BEGUN. THE MAP WILL CHANGE IN 30 SECONDS</color></b>", 10000);
					break;
			}
		}

		private void AddPlayer(byte playerID) {
			MultiplayerRemotePlayerController newPlayer = new MultiplayerRemotePlayerController(this.debugWriter);
			newPlayer.ConstructPlayer();
			newPlayer.playerID = playerID;
			this.remoteControllers.Add(newPlayer);
		}

		private void RemovePlayer(byte playerID) {
			MultiplayerRemotePlayerController remotePlayer = this.remoteControllers.Find(p => p.playerID == playerID);
			this.remoteControllers.Remove(remotePlayer);
			UnityEngine.Object.Destroy(remotePlayer.skater);
			UnityEngine.Object.Destroy(remotePlayer.board);
		}

		public void SendUpdate() {
			Tuple<byte[], bool> animationData = this.playerController.PackAnimations();

			if (animationData == null) return;

			// Only send update if it's new transforms(don't waste bandwidth on duplicates)
			if (this.playerController.currentAnimationTime != previousSentAnimationTime || 
				GameManagement.GameStateMachine.Instance.CurrentState.GetType() != typeof(GameManagement.PlayState)) {

				SendBytesAnimation(animationData.Item1, animationData.Item2);
				previousSentAnimationTime = this.playerController.currentAnimationTime;
			}
		}

		// For things that encode the prebuffer during serialization
		public void SendBytesRaw(byte[] msg, bool reliable) {
			client.SendMessageToConnection(connection, msg, reliable ? SendType.Reliable : SendType.Unreliable);
		}

		public void SendBytes(OpCode opCode, byte[] msg, bool reliable) {
			// Send bytes
			byte[] sendMsg = new byte[msg.Length + 1];
			sendMsg[0] = (byte)opCode;
			Array.Copy(msg, 0, sendMsg, 1, msg.Length);
			client.SendMessageToConnection(connection, sendMsg, reliable ? SendType.Reliable : SendType.Unreliable);
		}

		private void SendBytesAnimation(byte[] msg, bool reliable) {
			byte[] packetSequence = BitConverter.GetBytes(sentAnimUpdates);
			byte[] packetData = Compress(msg);
			byte[] packet = new byte[packetData.Length + packetSequence.Length + 2];

			packet[0] = (byte)OpCode.Animation;
			Array.Copy(packetSequence, 0, packet, 1, 4);
			Array.Copy(packetData, 0, packet, 5, packetData.Length);
			packet[packet.Length - 1] = reliable ? (byte)1 : (byte)0;

			client.SendMessageToConnection(connection, packet, reliable ? SendType.Reliable : SendType.Unreliable);

			sentAnimUpdates++;
		}

		public void SendChatMessage(string msg) {
			string afterMarkup = RemoveMarkup(msg);

			if(afterMarkup.Length > 0) {
				chatMessages.Add("<b><color=\"blue\">You: </color></b>" + afterMarkup);
				this.SendBytes(OpCode.Chat, ASCIIEncoding.ASCII.GetBytes(afterMarkup), true);
			}
		}

		private string RemoveMarkup(string msg) {
			string old;

			do {
				old = msg;
				msg = Regex.Replace(msg.Trim(), "</?(?:b|i|color|size|material|quad)[^>]*>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			} while (!msg.Equals(old));

			return msg;
		}

		public static byte[] Compress(byte[] data) {
			MemoryStream output = new MemoryStream();
			using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal)) {
				dstream.Write(data, 0, data.Length);
			}
			return output.ToArray();
		}

		public static byte[] Decompress(byte[] data) {
			MemoryStream compressedStream = new MemoryStream(data);
			MemoryStream output = new MemoryStream();
			using (DeflateStream dstream = new DeflateStream(compressedStream, CompressionMode.Decompress)) {
				dstream.CopyTo(output);
			}
			return output.ToArray();
		}

		private void CleanupSockets() {
			Library.Deinitialize();

			client = null;
			status = null;
			netMessages = null;
		}

		public void DisconnectFromServer() {
			Main.menu.EndMultiplayer();
		}

		public void OnDestroy() {
			client.CloseConnection(connection);

			isConnected = false;
			sendingUpdates = false;

			foreach (MultiplayerRemotePlayerController controller in remoteControllers) {
				GameObject.Destroy(controller.skater);
				GameObject.Destroy(controller.board);
				GameObject.Destroy(controller.skaterMeshObjects);
				GameObject.Destroy(controller.player);
			}

			CleanupSockets();

			// Delete all temp assests
			if (Directory.Exists(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp")) {
				foreach (string dir in Directory.GetDirectories(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp")) {
					foreach (string file in Directory.GetFiles(dir)) {
						File.Delete(file);
					}
					Directory.Delete(dir);
				}
				Directory.Delete(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp");
			}

			this.debugWriter.Close();
		}
	}
}
