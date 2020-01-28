using Harmony12;
using ReplayEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Valve.Sockets;

// TODO: Maybe send meshes? They're pretty small my dude

namespace XLMultiplayer {
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

	class MultiplayerController : MonoBehaviour{
		// Valve Sockets stuff
		private NetworkingSockets client = null;
		private StatusCallback status = null;
		private uint connection;
		private const int maxMessages = 256;
		private NetworkingMessage[] netMessages;
		DebugCallback debugCallbackDelegate = null;

		private bool isConnected = false;

		private StreamWriter debugWriter;

		private MultiplayerLocalPlayerController playerController;
		private List<MultiplayerRemotePlayerController> remoteControllers = new List<MultiplayerRemotePlayerController>();

		private bool replayStarted = false;

		private int sentAnimUpdates = 0;
		private float previousSentAnimationTime = -1;

		private int tickRate = 30;

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
				} catch (Exception e) {
					this.debugWriter = null;
					i++;
				}
			}
			this.debugWriter.AutoFlush = true;
			this.debugWriter.WriteLine("Attempting to connect to server ip {0} on port {1}", ip, port.ToString());

			MultiplayerUtils.serverMapDictionary.Clear();
			
			this.playerController = new MultiplayerLocalPlayerController(debugWriter);
			this.playerController.ConstructPlayer();
			this.playerController.username = user;

			Library.Initialize();

			client = new NetworkingSockets();

			Address remoteAddress = new Address();
			remoteAddress.SetAddress(ip, port);

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
					DisconnectFromServer();
					break;

				case ConnectionState.ProblemDetectedLocally:
					//Client unable to connect
					this.debugWriter.WriteLine("Unable to connect to server");
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

			#if DEBUG
			unsafe {
				// From data I saw a typical example of udp packets over the internet would be 0.3% loss, 25% reorder
				// For testing I'll use 25% reorder, 100ms delay on reorder, 150ms ping, and 2% loss

				// Re-order 25% of packets and add 200ms delay on reordered packets
				float reorderPercent = 25f;
				int reorderTime = 100;
				utils.SetConfiguratioValue(ConfigurationValue.FakePacketReorderSend, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Float, new IntPtr(&reorderPercent));
				utils.SetConfiguratioValue(ConfigurationValue.FakePacketReorderTime, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&reorderTime));

				// Fake 150ms ping
				int pingTime = 150;
				utils.SetConfiguratioValue(ConfigurationValue.FakePacketLagSend, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&pingTime));

				// Simulate 2% packet loss
				float lossPercent = 2f;
				utils.SetConfiguratioValue(ConfigurationValue.FakePacketLossSend, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Float, new IntPtr(&lossPercent));
			}
			#endif

			//byte[] usernameBytes = Encoding.ASCII.GetBytes(this.playerController.username);

			// TODO: Send username bytes

			// TODO: Start new send alive thread(Is this necessary? Maybe put UpdateClient on new thread?)

			// TODO: Send version number before updates and stuff

			//updateThread = new Thread(new ThreadStart(UpdateThreadFunction));
			//updateThread.IsBackground = true;
			//updateThread.Start();

			InvokeRepeating("SendUpdate", 0f, 1f / (float)this.tickRate);
			
			//this.playerController.EncodeTextures();

			//SendTextures();
		}

		public void StartLoadMap(string path) {
			this.StartCoroutine(LoadMap(path));
		}

		public IEnumerator LoadMap(string path) {
			while (!IsInvoking("SendUpdate")) {
				yield return new WaitForEndOfFrame();
			}
			//Load map with path
			LevelSelectionController levelSelectionController = GameManagement.GameStateMachine.Instance.LevelSelectionObject.GetComponentInChildren<LevelSelectionController>();
			GameManagement.GameStateMachine.Instance.LevelSelectionObject.SetActive(true);
			LevelInfo target = levelSelectionController.Levels.Find(level => level.path.Equals(path));
			if (target == null) {
				target = levelSelectionController.CustomLevels.Find(level => level.path.Equals(path));
			}
			if (target == null) {
				Main.statusMenu.DisplayNoMap(path);
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

		private void SendTextures() {
			// Send this
			// playerController.shirtMPTex.GetSendData();
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

			UpdateClient();

			// Lerp frames using frame buffer
			foreach (MultiplayerRemotePlayerController controller in this.remoteControllers) {
				if (controller != null) {
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

			// Save textures from queue

			// Apply saved textures
		}

		private void ProcessMessage(byte[] buffer) {
			OpCode opCode = (OpCode)buffer[0];
			byte playerID = buffer[buffer.Length - 1];

			byte[] newBuffer = new byte[buffer.Length - 2];

			if(newBuffer.Length != 0) {
				Array.Copy(buffer, 1, newBuffer, 0, buffer.Length - 2);
			}

			this.debugWriter.WriteLine("Recieved message with opcode {0}, and length {1}", opCode, buffer.Length);

			switch (opCode) {
				case OpCode.Connect:
					AddPlayer(playerID);
					break;
				case OpCode.Disconnect:
					RemovePlayer(playerID);
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

		private void UpdateThreadFunction() {
			while (isConnected) {
				SendUpdate();

				Thread.Sleep((int)((1f/(float)this.tickRate)*1000));
			}
		}

		public void SendUpdate() {
			Tuple<byte[], bool> animationData = this.playerController.PackAnimations();

			// Only send update if it's new transforms(don't waste bandwidth on duplicates)
			if (this.playerController.currentAnimationTime != previousSentAnimationTime || 
				GameManagement.GameStateMachine.Instance.CurrentState.GetType() != typeof(GameManagement.PlayState)) {

				SendBytesAnimation(animationData.Item1, animationData.Item2);
				previousSentAnimationTime = this.playerController.currentAnimationTime;
			}
		}

		// For things that encode the prebuffer during serialization
		public void SendBytesRaw(byte[] msg, bool reliable) {
		}

		public void SendBytes(OpCode opCode, byte[] msg, bool reliable) {
			// Send bytes
			byte[] sendMsg = new byte[msg.Length + 1];
			sendMsg[0] = (byte)opCode;
			Array.Copy(msg, 0, sendMsg, 1, msg.Length);
			client.SendMessageToConnection(connection, msg, reliable ? SendType.Reliable : SendType.Unreliable);
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
			client.CloseConnection(connection);

			isConnected = false;

			CancelInvoke("SendUpdate");

			CleanupSockets();

			UnityEngine.Object.Destroy(this);
		}

		public void OnDestroy() {
			DisconnectFromServer();
		}
	}
}
