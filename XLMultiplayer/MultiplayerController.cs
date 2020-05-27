#define VALVESOCKETS_SPAN

using Newtonsoft.Json;
using ReplayEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Sockets;
using XLShredLib;
using XLShredLib.UI;

// TODO LIST FOR 0.8.0

// TODO: Redo the multiplayer texture system
//			-> Send paths for non-custom gear
//			-> Send hashes of full size textures for custom gear along with compressed texture
//			-> Only send hashes/paths from server unless client requests texture data

// TODO: Create 2 connections -> One for gameplay 
//								-> One for files

// TODO: Fix dawgs board mod for mp

namespace XLMultiplayer {
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

	class CustomUsername {
		[JsonProperty("Secret_Key")]
		public static string secretKey = "";
		[JsonProperty("Username")]
		public static string username = "";
	}

	class MultiplayerController : MonoBehaviour{
		// Valve Sockets stuff
		private NetworkingSockets client = null;
		private StatusCallback status = null;
		private uint connection;

#if VALVESOCKETS_SPAN
		MessageCallback messageCallback = MessageCallbackHandler;
#else
		private const int maxMessages = 256;
		private NetworkingMessage[] netMessages;
#endif

		DebugCallback debugCallbackDelegate = null;

		private bool closedByPeer = false;
		private bool problemDetectedLocally = false;

		private Thread networkMessageThread;

		public List<string> chatMessages = new List<string>();

		public bool isConnected { get; private set; } = false;

		private List<byte[]> networkMessageQueue = new List<byte[]>();

		public StreamWriter debugWriter;

		private MultiplayerLocalPlayerController playerController;
		public List<MultiplayerRemotePlayerController> remoteControllers = new List<MultiplayerRemotePlayerController>();

		private bool replayStarted = false;

		private int sentAnimUpdates = 0;
		private float previousSentAnimationTime = -1;

		private int tickRate = 30;

		private bool sendingUpdates = false;
		private float timeSinceLastUpdate = 0.0f;
		
		private int alivePacketCount = 0;
		private int receivedAlivePackets = 0;
		private float lastAliveTime = 0;

		private float statisticsResetTime = 0.0f;
		private List<float> pingTimes = new List<float>();
		private int receivedAlive10Seconds = 0;
		private int sentAlive10Seconds = 0;

		private ModUIBox uiBox;

		private GUIStyle modMenuStyle = null;

		private string playerListText = "Player List Test";

		private string[] usernameColumnText = new string[3];
		private List<string> column1Usernames = new List<string>();
		private List<string> column2Usernames = new List<string>();
		private List<string> column3Usernames = new List<string>();

		private string networkStatsText = "Network Stats Will update after 10 seconds of connection";

		private string playerListLocalUser = "";

		private Thread usernameThread;

		private byte[] usernameMessage = null;

		public static MultiplayerController Instance { get; private set; }

		// Open replay editor on start to prevent null references to replay editor instance
		public void Start() {
			MultiplayerController.Instance = this;

			if (ReplayEditorController.Instance == null) {
				GameManagement.GameStateMachine.Instance.ReplayObject.SetActive(true);
				StartCoroutine(TurnOffReplay());
			}

			uiBox = ModMenu.Instance.RegisterModMaker("Silentbaws", "Silentbaws", 5);
			uiBox.AddCustom("Player List", PlayerListOnGUI, () => isConnected);
			uiBox.AddCustom("Network Stats", NetworkStatsOnGUI, () => isConnected);
		}

		private void InitializeStyle() {
			modMenuStyle = new GUIStyle();
			modMenuStyle.alignment = TextAnchor.UpperCenter;
			modMenuStyle.wordWrap = true;
			modMenuStyle.normal.textColor = Color.yellow;
			modMenuStyle.fontSize = 12;
			modMenuStyle.richText = true;
		}

		private void PlayerListOnGUI() {
			if (modMenuStyle == null) InitializeStyle();

			int desiredHeight = (int)modMenuStyle.CalcHeight(new GUIContent("Player List"), 1000);
			desiredHeight = (int)Math.Ceiling(desiredHeight * 1.5f);
			playerListText = $"<b><size={desiredHeight}>Player List</size></b>\n";

			if (playerController == null) return;
			if (column1Usernames.Count + column2Usernames.Count + column3Usernames.Count != remoteControllers.Count + 1 || playerListLocalUser != playerController.username) {
				int column = 0;

				column1Usernames.Clear();
				column2Usernames.Clear();
				column3Usernames.Clear();

				column2Usernames.Add(playerController.username + "(YOU)");
				playerListLocalUser = playerController.username;

				foreach (MultiplayerRemotePlayerController controller in remoteControllers) {
					switch (column) {
						case 0:
							column1Usernames.Add(controller.username + $"({controller.playerID})");
							break;
						case 1:
							column2Usernames.Add(controller.username + $"({controller.playerID})");
							break;
						case 2:
							column3Usernames.Add(controller.username + $"({controller.playerID})");
							column = -1;
							break;
					}

					column++;
				}

				// Move usernames in columns to prioritize middle and then left/right balance
				if (column1Usernames.Count > column3Usernames.Count && column1Usernames.Count > column2Usernames.Count) {
					column2Usernames.Add(column1Usernames[column1Usernames.Count - 1]);
					column1Usernames.RemoveAt(column1Usernames.Count - 1);
				} else if (column1Usernames.Count > column3Usernames.Count && column1Usernames.Count == column2Usernames.Count) {
					column3Usernames.Add(column2Usernames[column2Usernames.Count - 1]);
					column2Usernames.RemoveAt(column2Usernames.Count - 1);
				}

				for (int i = 0; i < 3; i++) {
					usernameColumnText[i] = "";
				}

				foreach (string username in column1Usernames) {
					usernameColumnText[0] += username + "\n";
				}
				foreach (string username in column2Usernames) {
					usernameColumnText[1] += username + "\n";
				}
				foreach (string username in column3Usernames) {
					usernameColumnText[2] += username + "\n";
				}
			}

			GUILayout.Label(playerListText, modMenuStyle, null);

			GUILayout.BeginHorizontal();

			// Split usernames into 3 rows
			GUILayout.BeginVertical(ModMenu.Instance.columnLeftStyle, GUILayout.Width(ModMenu.label_column_width * 2 / 3));
			GUILayout.Label(usernameColumnText[0], modMenuStyle, null);
			GUILayout.EndVertical();

			GUILayout.BeginVertical(modMenuStyle, GUILayout.Width(ModMenu.label_column_width * 2 / 3));
			GUILayout.Label(usernameColumnText[1], modMenuStyle, null);
			GUILayout.EndVertical();

			GUILayout.BeginVertical(GUILayout.Width(ModMenu.label_column_width * 2 / 3));
			GUILayout.Label(usernameColumnText[2], modMenuStyle, null);
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();
		}

		private void NetworkStatsOnGUI() {
			if (modMenuStyle == null) InitializeStyle();

			GUILayout.Label(networkStatsText, modMenuStyle, null);
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
			if (!IPAddress.TryParse(ip, out serverIP)) {
				try {
					IPHostEntry hostInfo = Dns.GetHostEntry(ip);
					foreach (IPAddress address in hostInfo.AddressList) {
						if (address.MapToIPv4() != null) {
							serverIP = address.MapToIPv4();
						}
					}
				} catch (Exception) { }
			}


			Address remoteAddress = new Address();
			remoteAddress.SetAddress(serverIP.ToString(), port);

			connection = client.Connect(ref remoteAddress);

			status = StatusCallback;

#if !VALVESOCKETS_SPAN
			netMessages = new NetworkingMessage[maxMessages];
#endif

			networkMessageThread = new Thread(UpdateClient);
			networkMessageThread.IsBackground = true;
			networkMessageThread.Start();
		}

		private void StatusCallback(ref StatusInfo info, IntPtr context) {
			switch (info.connectionInfo.state) {
				case ConnectionState.None:
					break;

				case ConnectionState.Connected:
					ConnectionCallback();
					break;

				case ConnectionState.ClosedByPeer:
					//Client disconnected from server
					closedByPeer = true;
					break;

				case ConnectionState.ProblemDetectedLocally:
					//Client unable to connect
					problemDetectedLocally = true;
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

			unsafe {
#if DEBUG
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
#endif

				int sendRateMin = 0;
				int sendRateMax = 209715200;
				int sendBufferSize = 10485760;
				utils.SetConfigurationValue(ConfigurationValue.SendRateMin, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendRateMin));
				utils.SetConfigurationValue(ConfigurationValue.SendRateMax, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendRateMax));
				utils.SetConfigurationValue(ConfigurationValue.SendBufferSize, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&sendBufferSize));
			}
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
			LevelInfo target = LevelManager.Instance.Levels.Find(level => level.path.Equals(path));
			if (target == null) {
				target = LevelManager.Instance.CustomLevels.Find(level => level.path.Equals(path));
			}
			LevelManager.Instance.LoadLevel(target);
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
			if (closedByPeer) {
				//Client disconnected from server
				this.debugWriter.WriteLine("Disconnected from server");
				Main.utilityMenu.SendImportantChat("<b><color=\"red\">You have been disconnected from the server</color></b>", 7500);
				DisconnectFromServer();
			}

			if (problemDetectedLocally) {
				//Client unable to connect
				this.debugWriter.WriteLine("Unable to connect to server");
				Main.utilityMenu.SendImportantChat("<b><color=\"red\">You could not connect to the server</color></b>", 7500);
				DisconnectFromServer();
			}

			if (client == null) return;

			if (usernameMessage != null) {
				SendBytes(OpCode.Settings, usernameMessage, true, true);
				usernameMessage = null;
			}

			if (GameManagement.GameStateMachine.Instance.CurrentState.GetType() != typeof(GameManagement.ReplayState)) {
				if (replayStarted) {
					foreach (MultiplayerRemotePlayerController controller in remoteControllers) {
						if (controller.playerID == 255) {
							controller.skater.SetActive(false);
							controller.board.SetActive(false);
						}

						controller.EndReplay();
					}
				}
				replayStarted = false;
			} else {
				if (!replayStarted) {
					replayStarted = true;

					foreach (MultiplayerRemotePlayerController controller in remoteControllers) {
						if (controller.playerID == 255) {
							controller.skater.SetActive(true);
							controller.board.SetActive(true);
						}

						controller.PrepareReplay();
					}
				}
			}

			// Don't allow use of map selection
			if (GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.LevelSelectionState) && MultiplayerUtils.serverMapDictionary.Count > 0 && isConnected) {
				GameManagement.GameStateMachine.Instance.RequestPlayState();
			}
			
			if (sendingUpdates) {
				this.playerController.SendTextures();

				timeSinceLastUpdate += Time.unscaledDeltaTime;
				if (timeSinceLastUpdate > 1f / (float)tickRate) {
					SendUpdate();
					timeSinceLastUpdate = 0.0f;
				}
			}
			
			// Calculate network statistics every 10 seconds
			if (Time.time - statisticsResetTime > 10f && pingTimes.Count > 0 && sentAlive10Seconds > 0) {
				float totalPing = 0;
				foreach (float ping in pingTimes) {
					totalPing += ping;
				}

				int realPing = (int)(totalPing / pingTimes.Count * 1000f);
				float lossPercent = Mathf.Max(((1f - ((float)receivedAlive10Seconds / (float)sentAlive10Seconds)) * 100f), 0);

				string netstats = $"Ping: {realPing}ms           Packet Loss: {lossPercent.ToString("N2")}%";

				this.debugWriter.WriteLine(netstats);
				networkStatsText = netstats;

				statisticsResetTime = Time.time;
				sentAlive10Seconds = 0;
				receivedAlive10Seconds = 0;
				pingTimes.Clear();
			}

			ProcessMessageQueue();

			// Lerp frames using frame buffer
			foreach (MultiplayerRemotePlayerController controller in this.remoteControllers) {
				if (controller != null) {
					controller.ApplyTextures();

					if (GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.ReplayState)) {
						controller.replayController.TimeScale = ReplayEditorController.Instance.playbackController.TimeScale;
						controller.replayController.SetPlaybackTime(ReplayEditorController.Instance.playbackController.CurrentTime);


						if (controller.playerID == 255 && ((controller.replayController.ClipFrames.Last().time < ReplayEditorController.Instance.playbackController.CurrentTime && controller.skater.activeSelf) || (controller.replayController.ClipFrames.Count == 0 && controller.skater.activeSelf))) {
							controller.skater.SetActive(false);
							controller.board.SetActive(false);
						} else if (controller.playerID == 255 && controller.replayController.ClipFrames.Count > 0 && controller.replayController.ClipFrames.Last().time > ReplayEditorController.Instance.playbackController.CurrentTime && !controller.skater.activeSelf) {
							controller.skater.SetActive(true);
							controller.board.SetActive(true);
						}
					}

					if (controller.playerID == 255 && controller.replayAnimationFrames.Last(obj => obj.realFrameTime != -1f).realFrameTime < ReplayRecorder.Instance.RecordedFrames.First().time) {
						RemovePlayer(controller);
					}

					controller.LerpNextFrame(GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.ReplayState));
				}
			}
		}
		
		private void ProcessMessageQueue() {
			for (int i = networkMessageQueue.Count; i > 0; i--) {
				byte[] message = null;
				if (networkMessageQueue[0] != null) {
					message = new byte[networkMessageQueue[0].Length];
					networkMessageQueue[0].CopyTo(message, 0);
				}
				networkMessageQueue.RemoveAt(0);
				if (message != null) {
					ProcessMessage(message);
				}
			}
		}

#if VALVESOCKETS_SPAN
		private static void MessageCallbackHandler(in NetworkingMessage netMessage) {
			byte[] messageData = new byte[netMessage.length];
			netMessage.CopyTo(messageData);

			MultiplayerController.Instance.networkMessageQueue.Add(messageData);
		}
#endif

		private void UpdateClient() {
			statisticsResetTime = Time.time;
			lastAliveTime = Time.time;
			while (this.playerController != null) {
				if(client != null) {
					client.DispatchCallback(status);

					if (debugCallbackDelegate != null)
						GC.KeepAlive(debugCallbackDelegate);

					if (messageCallback != null)
						GC.KeepAlive(messageCallback);

					if (client != null)
						GC.KeepAlive(client);

					if (isConnected && Time.time - lastAliveTime >= 0.2f) {
						byte[] currentTime = BitConverter.GetBytes(Time.time);
						byte[] message = new byte[5];
						message[0] = (byte)OpCode.StillAlive;
						Array.Copy(currentTime, 0, message, 1, 4);

						SendBytesRaw(message, false, true);

						alivePacketCount++;
						sentAlive10Seconds++;
						lastAliveTime = Time.time;
					}

#if VALVESOCKETS_SPAN
					client.ReceiveMessagesOnConnection(connection, messageCallback, 256);
#else
					int netMessagesCount = client.ReceiveMessagesOnConnection(connection, netMessages, maxMessages);

					if (netMessagesCount > 0) {
						for (int i = 0; i < netMessagesCount; i++) {
							ref NetworkingMessage netMessage = ref netMessages[i];

							

							netMessage.Destroy();
						}
					}
#endif
				}
			}
		}

		private void ProcessMessage(byte[] buffer) {
			OpCode opCode = (OpCode)buffer[0];
			byte playerID = buffer[buffer.Length - 1];

			byte[] newBuffer = new byte[buffer.Length - 2];

			if (newBuffer.Length != 0) {
				Array.Copy(buffer, 1, newBuffer, 0, buffer.Length - 2);
			}

			if (opCode != OpCode.Animation && opCode != OpCode.StillAlive) this.debugWriter.WriteLine("Recieved message with opcode {0}, and length {1}", opCode, buffer.Length);

			switch (opCode) {
				case OpCode.Connect:
					AddPlayer(playerID);
					break;
				case OpCode.Disconnect:
					// TODO: Delay destruction and removal until after they're no longer in replay
					MultiplayerRemotePlayerController player = remoteControllers.Find(c => c.playerID == playerID);
					chatMessages.Add("Player <color=\"yellow\">" + player.username + "{" + player.playerID + "}</color> <b><color=\"red\">DISCONNECTED</color></b>");
					if(player.replayAnimationFrames.Count > 5) {
						player.playerID = 255;
						player.skater.SetActive(false);
						player.board.SetActive(false);
						player.usernameObject.SetActive(false);
					} else {
						RemovePlayer(player);
					}
					break;
				case OpCode.VersionNumber:
					string serverVersion = ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 1);

					if (serverVersion.Equals(Main.modEntry.Version.ToString())) {
						this.debugWriter.WriteLine("Server version matches client version start encoding");
						
						usernameThread = new Thread(SendUsername);
						usernameThread.IsBackground = true;
						usernameThread.Start();

						this.StartCoroutine(this.playerController.EncodeTextures());
						
						sendingUpdates = true;
					} else {
						this.debugWriter.WriteLine("server version {0} does not match client version {1}", serverVersion, Main.modEntry.Version.ToString());
						Main.utilityMenu.SendImportantChat("<color=\"yellow\">server version <color=\"green\"><b>" + serverVersion + "</b></color> does not match client version <color=\"red\"><b>" + Main.modEntry.Version.ToString() + "</b></color></color>", 7500);
						DisconnectFromServer();
					}
					break;
				case OpCode.Settings:
					MultiplayerRemotePlayerController remotePlayer = remoteControllers.Find((p) => { return p.playerID == playerID; });
					if (remotePlayer != null) {
						remotePlayer.username = ASCIIEncoding.ASCII.GetString(newBuffer);
						if (!ContainsMarkup(remotePlayer.username)) {
							chatMessages.Add("Player <color=\"yellow\">" + remotePlayer.username + "{" + remotePlayer.playerID + "}</color> <b><color=\"green\">CONNECTED</color></b>");
						} else {
							chatMessages.Add("Player " + remotePlayer.username + "{" + remotePlayer.playerID + "} <b><color=\"green\">CONNECTED</color></b>");
						}
						if (column2Usernames.Count > 0) column2Usernames.RemoveAt(0);
						else if (column1Usernames.Count > 0) column1Usernames.RemoveAt(0);
						else if (column3Usernames.Count > 0) column3Usernames.RemoveAt(0);
					}
					break;
				case OpCode.UsernameAdjustment:
					this.playerController.username = ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 1);
					Main.utilityMenu.SendImportantChat($"<color=\"yellow\">Server adjusted your username to {this.playerController.username}</color>", 5000);
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
				case OpCode.StillAlive:
					float sentTime = BitConverter.ToSingle(buffer, 1);
					pingTimes.Add(Time.time - sentTime);
					receivedAlivePackets++;
					receivedAlive10Seconds++;
					break;
			}
		}

		private void AddPlayer(byte playerID) {
			MultiplayerRemotePlayerController newPlayer = new MultiplayerRemotePlayerController(this.debugWriter);
			newPlayer.ConstructPlayer();
			newPlayer.playerID = playerID;
			this.remoteControllers.Add(newPlayer);
		}

		private void RemovePlayer(MultiplayerRemotePlayerController remotePlayer) {
			this.remoteControllers.Remove(remotePlayer);
			this.debugWriter.WriteLine("Deleting player");
			remotePlayer.Destroy();
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
		public void SendBytesRaw(byte[] msg, bool reliable, bool nonagle = false, bool nodelay = false) {
			SendFlags sendType = reliable ? SendFlags.Reliable : SendFlags.Unreliable;
			if (nonagle) sendType |= SendFlags.NoNagle;
			if (nodelay) sendType |= SendFlags.NoDelay;
			if (client != null) client.SendMessageToConnection(connection, msg, sendType);
		}

		public void SendBytes(OpCode opCode, byte[] msg, bool reliable, bool nonagle = false) {
			// Send bytes
			SendFlags sendType = reliable ? SendFlags.Reliable : SendFlags.Unreliable;
			if (nonagle) sendType = sendType | SendFlags.NoNagle;

			byte[] sendMsg = new byte[msg.Length + 1];
			sendMsg[0] = (byte)opCode;
			Array.Copy(msg, 0, sendMsg, 1, msg.Length);
			client.SendMessageToConnection(connection, sendMsg, sendType);
		}

		private void SendBytesAnimation(byte[] msg, bool reliable) {
			byte[] packetSequence = BitConverter.GetBytes(sentAnimUpdates);
			byte[] packetData = Compress(msg);
			byte[] packet = new byte[packetData.Length + packetSequence.Length + 2];

			packet[0] = (byte)OpCode.Animation;
			Array.Copy(packetSequence, 0, packet, 1, 4);
			Array.Copy(packetData, 0, packet, 5, packetData.Length);
			packet[packet.Length - 1] = reliable ? (byte)1 : (byte)0;

			client.SendMessageToConnection(connection, packet, reliable ? SendFlags.Reliable : SendFlags.Unreliable);

			sentAnimUpdates++;
		}

		public void SendUsername() {
			if (File.Exists(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\" + "CustomUsername.json")) {
				JsonConvert.DeserializeObject<CustomUsername>(File.ReadAllText(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\" + "CustomUsername.json"));

				const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
				string session = "";
				using (RNGCryptoServiceProvider cryptoProvider = new RNGCryptoServiceProvider()) {
					while(session.Length < 64) {
						byte[] randByte = new byte[1];
						cryptoProvider.GetBytes(randByte);
						char newChar = ASCIIEncoding.ASCII.GetString(randByte)[0];
						if (chars.Contains(newChar)) {
							session += newChar;
						}
					}
				}

				var values = new Dictionary<string, string> {
				{ "session", session } };
				
				// Try to give a session key instead of tying encryption key to IP(Useful if client has multiple IPs or something)

				string content = "";
				UnityWebRequest www = UnityWebRequest.Post("http://davisellwood-site.herokuapp.com/api/gettempkey/", values);

				www.SendWebRequest();

				while (!www.isDone) ;

				if (!www.isNetworkError && !www.isHttpError) {
					content = www.downloadHandler.text;
				} else {
					session = "";

					www = UnityWebRequest.Get("https://davisellwood-site.herokuapp.com/api/gettempkey/");
					www.SendWebRequest();

					while (!www.isDone) ;
					
					if (!www.isNetworkError && !www.isHttpError) {
						content = www.downloadHandler.text;
					}
				}

				this.playerController.username = CustomUsername.username;

				byte[] encryptionKey = ASCIIEncoding.ASCII.GetBytes(content);
				byte[] IV = new byte[0];
				byte[] usernameBytes = new byte[0];
				byte[] fancyBytes = ASCIIEncoding.ASCII.GetBytes(CustomUsername.username);
				byte[] sessionBytes = ASCIIEncoding.ASCII.GetBytes(session);

				if(encryptionKey.Length > 0) {
					using (Aes myAes = Aes.Create()) {
						myAes.Mode = CipherMode.CBC;
						IV = myAes.IV;
						usernameBytes = MultiplayerUtils.EncryptStringToBytes_Aes(CustomUsername.secretKey, encryptionKey, IV);
					}
				}

				byte[] message = new byte[IV.Length + usernameBytes.Length + fancyBytes.Length + sessionBytes.Length + 17];
				message[0] = 1;
				Array.Copy(BitConverter.GetBytes(IV.Length), 0, message, 1, 4);
				Array.Copy(IV, 0, message, 5, IV.Length);
				Array.Copy(BitConverter.GetBytes(usernameBytes.Length), 0, message, 5 + IV.Length, 4);
				Array.Copy(usernameBytes, 0, message, 9 + IV.Length, usernameBytes.Length);
				Array.Copy(BitConverter.GetBytes(fancyBytes.Length), 0, message, 9 + IV.Length + usernameBytes.Length, 4);
				Array.Copy(fancyBytes, 0, message, 13 + IV.Length + usernameBytes.Length, fancyBytes.Length);
				Array.Copy(BitConverter.GetBytes(sessionBytes.Length), 0, message, 13 + IV.Length + usernameBytes.Length + fancyBytes.Length, 4);
				Array.Copy(sessionBytes, 0, message, 17 + IV.Length + usernameBytes.Length + fancyBytes.Length, sessionBytes.Length);

				usernameMessage = message;
			} else {
				byte[] usernameBytes = Encoding.ASCII.GetBytes(this.playerController.username);
				byte[] message = new byte[usernameBytes.Length + 1];
				message[0] = 0;
				Array.Copy(usernameBytes, 0, message, 1, usernameBytes.Length);

				usernameMessage = message;
			}
		}

		public void SendChatMessage(string msg) {
			string afterMarkup = RemoveMarkup(msg);

			if(afterMarkup.Length > 0) {
				chatMessages.Add(this.playerController.username + "<b><color=\"blue\"> (You): </color></b>" + afterMarkup);
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

		private bool ContainsMarkup(string msg) {
			string old;
			
			old = msg;
			msg = Regex.Replace(msg.Trim(), "</?(?:b|i|color|size|material|quad)[^>]*>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);

			return old != msg;
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

#if !VALVESOCKETS_SPAN
			netMessages = null;
#endif
		}

		public void DisconnectFromServer() {
			Main.menu.EndMultiplayer();
		}

		public void OnDestroy() {
			isConnected = false;
			sendingUpdates = false;

			Main.utilityMenu.isLoading = false;

			this.playerController = null;

			if (client != null) client.CloseConnection(connection);

			foreach (MultiplayerRemotePlayerController controller in remoteControllers) {
				controller.Destroy();
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
			
			if (usernameThread != null && usernameThread.IsAlive) usernameThread.Abort();
			if (networkMessageThread != null && networkMessageThread.IsAlive) networkMessageThread.Abort();

			this.debugWriter.Close();
			this.debugWriter.Dispose();
		}
	}
}
