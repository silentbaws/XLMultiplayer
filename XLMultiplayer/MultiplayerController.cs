#define VALVESOCKETS_SPAN

using BFS.AnimationCombiner;
using GameManagement;
using HarmonyLib;
using Newtonsoft.Json;
using ReplayEditor;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Valve.Sockets;

// TODO: game of skate.... maybe?

// TODO: Make replays save audio

// TODO: Redo the multiplayer texture system
//			-> Send paths for non-custom gear

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
		ServerMessage = 10,
		Sound = 11,
		Plugin = 12,
		PluginHash = 13,
		PluginFile = 14,
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
		private NetworkingSockets fileClient = null;
		private StatusCallback status = null;
		private uint connection;
		private uint fileConnection;

		private uint serverConnectionNumber;

#if VALVESOCKETS_SPAN
		MessageCallback messageCallback = MessageCallbackHandler;
#else
		private const int maxMessages = 256;
		private NetworkingMessage[] netMessages;
#endif

		DebugCallback debugCallbackDelegate = null;

		private bool closedByPeer = false;
		private bool problemDetectedLocally = false;

		private string serverIPString = "";
		private ushort serverPort = 7777;

		private Thread networkMessageThread;
		private Thread decompressionThread;
		
		public List<string> chatMessages = new List<string>();

		public bool isConnected { get; private set; } = false;
		public bool isFileConnected { get; private set; } = false;

		private ConcurrentQueue<Tuple<byte[], uint>> networkMessageQueue = new ConcurrentQueue<Tuple<byte[], uint>>();
		private ConcurrentQueue<Tuple<byte, byte[]>> CompressedSounds = new ConcurrentQueue<Tuple<byte, byte[]>>();
		private ConcurrentQueue<Tuple<byte, byte[]>> DecompressedSounds = new ConcurrentQueue<Tuple<byte, byte[]>>();
		private ConcurrentQueue<Tuple<byte, byte[]>> CompressedAnimations = new ConcurrentQueue<Tuple<byte, byte[]>>();
		private ConcurrentQueue<Tuple<byte, byte[]>> DecompressedAnimations = new ConcurrentQueue<Tuple<byte, byte[]>>();

		public StreamWriter debugWriter;

		private MultiplayerLocalPlayerController playerController;
		public List<MultiplayerRemotePlayerController> remoteControllers = new List<MultiplayerRemotePlayerController>();

		private bool replayStarted = false;

		private int sentAnimUpdates = 0;
		private float previousSentAnimationTime = -1;

		private int tickRate = 30;

		private bool sendingUpdates = false;
		private float timeSinceLastUpdate = 0.0f;
		
		private float lastAliveTime = 0;

		private float statisticsResetTime = 0.0f;
		private List<float> pingTimes = new List<float>();
		private int receivedAlive10Seconds = 0;
		private int sentAlive10Seconds = 0;

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

		private Stopwatch FrameWatch = new Stopwatch();
		private double StateManagementTime = 0;
		private double NetworkDiagnosticTime = 0;
		private double MessageQueueTime = 0;
		private double SoundAndAnimationTime = 0;
		
		private List<double> previousQueueTimes = new List<double>();
		private List<Tuple<double, OpCode>> proccessedMessages = new List<Tuple<double, OpCode>>();

		private int messagesProcessed = 0;

		private float recentTimeSinceStartup = 0f;

		private bool initiatingServerConnection = false;
		private bool initiatingFileServerConnection = false;

		// Open replay editor on start to prevent null references to replay editor instance
		public void Start() {
			MultiplayerController.Instance = this;

			if (ReplayEditorController.Instance == null) {
				GameManagement.GameStateMachine.Instance.ReplayObject.SetActive(true);
				StartCoroutine(TurnOffReplay());
			}

			var clipDict = Traverse.Create(SoundManager.Instance).Field("clipForName").GetValue<Dictionary<string, AudioClip>>();
			MultiplayerUtils.InitializeClipToArrayByteDict(clipDict);

			MultiplayerUtils.audioPlayerNames.Clear();
			foreach (ReplayAudioEventPlayer audioPlayer in ReplayEditorController.Instance.playbackController.AudioEventPlayers) {
				AudioSource newSource = Traverse.Create(audioPlayer).Property("audioSource").GetValue<AudioSource>();
				if (newSource != null) MultiplayerUtils.audioPlayerNames.Add(newSource.name);
			}
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
					if (controller.playerID == 255) continue;

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

			Type skaterSpawner = AccessTools.TypeByName("SkaterSpawner");

			if (skaterSpawner != null) {
				object spawnerInstance = Traverse.Create(skaterSpawner).Property("Instance").GetValue();
				if (spawnerInstance != null) {
					Traverse.Create(spawnerInstance).Method("RemoveModelCoroutine").GetValue();
				}
			}

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

			// TODO: Add popup window with a download for vc_redistx64.exe
			
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

			serverPort = port;
			serverIPString = serverIP.ToString();

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

			decompressionThread = new Thread(DecompressSoundAnimationQueue);
			decompressionThread.IsBackground = true;
			decompressionThread.Start();
		}

		private void StatusCallback(ref StatusInfo info, IntPtr context) {
			switch (info.connectionInfo.state) {
				case ConnectionState.None:
					break;

				case ConnectionState.Connected:
					this.debugWriter.WriteLine("Got connected message");

					if (info.connection == connection) {
						initiatingServerConnection = true;
					} else if (info.connection == fileConnection) {
						initiatingFileServerConnection = true;
					}

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
				//int reorderTime = 50;
				//utils.SetConfigurationValue(ConfigurationValue.FakePacketReorderSend, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Float, new IntPtr(&reorderPercent));
				//utils.SetConfigurationValue(ConfigurationValue.FakePacketReorderTime, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&reorderTime));

				//Fake 150ms ping
				//int pingTime = 150;
				//utils.SetConfigurationValue(ConfigurationValue.FakePacketLagSend, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Int32, new IntPtr(&pingTime));

				//Simulate 0.2 % packet loss
				//float lossPercent = 0.2f;
				//utils.SetConfigurationValue(ConfigurationValue.FakePacketLossSend, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Float, new IntPtr(&lossPercent));
				//utils.SetConfigurationValue(ConfigurationValue.FakePacketLossRecv, ConfigurationScope.Global, IntPtr.Zero, ConfigurationDataType.Float, new IntPtr(&lossPercent));
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

			LevelInfo target = LevelManager.Instance.Levels.Find(level => level.path.Trim().Equals(path.Trim(), StringComparison.CurrentCultureIgnoreCase));
			if (target == null) {
				target = LevelManager.Instance.CustomLevels.Find(level => level.path.Trim().Equals(path.Trim(), StringComparison.CurrentCultureIgnoreCase));;
			}

			yield return new WaitWhile(() => GameStateMachine.Instance.IsLoading);
			if (!target.Equals(LevelManager.Instance.currentLevel)) {
				//Load map with path
				LevelSelectionController levelSelectionController = GameStateMachine.Instance.LevelSelectionObject.GetComponentInChildren<LevelSelectionController>();

				IndexPath targetIndex = GetIndexForLevel(target);
				Traverse.Create(levelSelectionController).Method("OnLevelHighlighted", targetIndex).GetValue();

				string texturePath = Path.ChangeExtension(target.path, "png");
				if (!File.Exists(texturePath)) {
					texturePath = Path.ChangeExtension(target.path, "jpg");
				}
				if (!File.Exists(texturePath)) {
					texturePath = Path.ChangeExtension(target.path, "jpeg");
				}

				if (File.Exists(texturePath)) {
					yield return new WaitWhile(() => target.previewImage == null);
				}

				levelSelectionController.OnItemSelected(targetIndex);

				yield return new WaitWhile(() => GameStateMachine.Instance.IsLoading);

				PlayerController.Instance.respawn.ForceRespawn();
			}
			yield break;
		}

		
		private IndexPath GetIndexForLevel(LevelInfo level) {
			if (level == null) {
				return new IndexPath(new int[2]);
			}
			int num = LevelManager.Instance.Levels.IndexOf(level);
			if (num >= 0) {
				return new IndexPath(new int[]
				{
				0,
				num
				});
			}
			num = LevelManager.Instance.CustomLevels.IndexOf(level);
			if (num >= 0) {
				return new IndexPath(new int[]
				{
				1,
				num
				});
			}
			return new IndexPath(new int[2]);
		}

		public void Update() {
			FrameWatch.Restart();

			recentTimeSinceStartup = Time.realtimeSinceStartup;

			if (networkMessageQueue != null) {
				GC.KeepAlive(networkMessageQueue);
				GC.KeepAlive(CompressedSounds);
				GC.KeepAlive(DecompressedSounds);
				GC.KeepAlive(CompressedAnimations);
				GC.KeepAlive(DecompressedAnimations);
			}

			if (GameStateMachine.Instance.CurrentState.GetType().Equals(typeof(PauseState))) {
				if (GameStateMachine.Instance.CurrentState.CanDoTransitionTo(typeof(ChallengeSelectionState))) {
					Type[] allowedTransitions = null;
					if (MultiplayerUtils.serverMapDictionary.Count == 0) {
						allowedTransitions = new Type[4];
						allowedTransitions[3] = typeof(LevelSelectionState);
					} else {
						allowedTransitions = new Type[3];
					}
					allowedTransitions[0] = typeof(PlayState);
					allowedTransitions[1] = typeof(ReplayMenuState);
					allowedTransitions[2] = typeof(SettingsState);
					Traverse.Create(GameStateMachine.Instance.CurrentState).Field("availableTransitions").SetValue( allowedTransitions );
				}
			}
			
			if (initiatingServerConnection) {
				ConnectionCallback();
				this.StartCoroutine(this.playerController.EncodeTextures());
				initiatingServerConnection = false;
			} else if (initiatingFileServerConnection) {
				this.debugWriter.WriteLine("connected on file server");

				byte[] connectMessage = new byte[5];
				connectMessage[0] = (byte)OpCode.Connect;
				Array.Copy(BitConverter.GetBytes(serverConnectionNumber), 0, connectMessage, 1, 4);

				fileClient.SendMessageToConnection(fileConnection, connectMessage, SendFlags.Reliable | SendFlags.NoNagle);
				isFileConnected = true;
				initiatingFileServerConnection = false;
			}

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

			if (GameStateMachine.Instance.CurrentState.GetType() != typeof(ReplayState)) {
				if (replayStarted) {
					foreach (MultiplayerRemotePlayerController controller in remoteControllers) {
						if (controller.playerID == 255) {
							controller.skater.SetActive(false);
							controller.board.SetActive(false);
						}

						controller.EndReplay();

						foreach (ReplayAudioEventPlayer replayAudioPlayer in controller.replayController.AudioEventPlayers) {
							if (replayAudioPlayer != null) {
								Traverse.Create(replayAudioPlayer).Method("UnloadEvents").GetValue();
							}
						}
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

			recentTimeSinceStartup = Time.realtimeSinceStartup;

			StateManagementTime = FrameWatch.Elapsed.TotalMilliseconds;
			
			if (sendingUpdates) {
				this.playerController.SendTextures();

				timeSinceLastUpdate += Time.unscaledDeltaTime;
				if (timeSinceLastUpdate > 1f / (float)tickRate) {
					SendUpdate();
					timeSinceLastUpdate = 0.0f;
				}
			}

			recentTimeSinceStartup = Time.realtimeSinceStartup;

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

			NetworkDiagnosticTime = FrameWatch.Elapsed.TotalMilliseconds - StateManagementTime;
			
			ProcessMessageQueue();

			MessageQueueTime = FrameWatch.Elapsed.TotalMilliseconds - NetworkDiagnosticTime;

			ProcessSoundAnimationQueue();

			double SoundAnimationTime = FrameWatch.Elapsed.TotalMilliseconds - MessageQueueTime;

			recentTimeSinceStartup = Time.realtimeSinceStartup;

			// Lerp frames using frame buffer
			List<MultiplayerRemotePlayerController> controllerToRemove = new List<MultiplayerRemotePlayerController>();

			foreach (MultiplayerRemotePlayerController controller in this.remoteControllers) {
				if (controller != null) {
					controller.ApplyTextures();
					
					if (replayStarted && GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.ReplayState)) {
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

					if (controller.playerID == 255 && (ReplayRecorder.Instance.RecordedFrames.Count < 1 || controller.replayAnimationFrames.LastOrDefault(obj => obj.realFrameTime != -1f) == null || (controller.replayAnimationFrames.Last(obj => obj.realFrameTime != -1f).realFrameTime < ReplayRecorder.Instance.RecordedFrames.First().time))) {
						controllerToRemove.Add(controller);
					}

					// TODO: Perform calculations on seperate thread and then apply transformations on main thread
					controller.StartFrameLerp();
				}
				recentTimeSinceStartup = Time.realtimeSinceStartup;
			}

			foreach (MultiplayerRemotePlayerController player in controllerToRemove)
				RemovePlayer(player);

			foreach (MultiplayerRemotePlayerController controller in this.remoteControllers) {
				controller.EndLerpFrame();
			}
			SoundAndAnimationTime = FrameWatch.Elapsed.TotalMilliseconds - MessageQueueTime;

			//private double StateManagementTime = 0;
			//private double NetworkDiagnosticTime = 0;
			//private double MessageQueueTime = 0;
			//private double SoundAndAnimationTime = 0;

			FrameWatch.Stop();
			previousQueueTimes.Add(MessageQueueTime);

			double averageTime = 0;
			if(previousQueueTimes.Count > 100) {
				previousQueueTimes.RemoveAt(previousQueueTimes.Count - 1);
				foreach(double t in previousQueueTimes) {
					averageTime += t;
				}

				averageTime /= previousQueueTimes.Count;

				if(MessageQueueTime > averageTime * 1.5) {
					this.debugWriter.WriteLine($"Frame Time Multiplayer: {FrameWatch.Elapsed.TotalMilliseconds} - Sound and Animation Queue Processing {SoundAndAnimationTime}, State management {StateManagementTime}, Network Diagnostics {NetworkDiagnosticTime}, Message Queue Processing {MessageQueueTime}, Sound and Animations {SoundAndAnimationTime}, Messages Processed {messagesProcessed}");
					foreach(Tuple<double, OpCode> item in proccessedMessages) {
						this.debugWriter.WriteLine($"Opcode {item.Item2} took {item.Item1}ms");
					}
				}
			}

			recentTimeSinceStartup = Time.realtimeSinceStartup;
		}
		
		private void ProcessMessageQueue() {
			proccessedMessages.Clear();
			messagesProcessed = 0;

			while (!networkMessageQueue.IsEmpty) {
				messagesProcessed++;
				Tuple<byte[], uint> currentMessage;
				if (networkMessageQueue.TryDequeue(out currentMessage)) {
					ProcessMessage(currentMessage.Item1, currentMessage.Item2);
				}

				recentTimeSinceStartup = Time.realtimeSinceStartup;
				currentMessage = null;
			}
		}

#if VALVESOCKETS_SPAN
		private static void MessageCallbackHandler(in NetworkingMessage netMessage) {
			byte[] messageData = new byte[netMessage.length];
			netMessage.CopyTo(messageData);
			
			if (Instance.networkMessageQueue == null) {
				Instance.networkMessageQueue = new ConcurrentQueue<Tuple<byte[], uint>>();
			}

			Instance.networkMessageQueue.Enqueue(Tuple.Create(messageData, netMessage.connection));
		}
#endif

		private void UpdateClient() {
			statisticsResetTime = recentTimeSinceStartup;
			lastAliveTime = recentTimeSinceStartup;
			while (this.playerController != null) {
				SpinWait.SpinUntil(() => !isConnected, 6);
				if (client != null) {
					client.DispatchCallback(status);

					if (debugCallbackDelegate != null)
						GC.KeepAlive(debugCallbackDelegate);

					if (messageCallback != null)
						GC.KeepAlive(messageCallback);

					if (client != null)
						GC.KeepAlive(client);

					if (isConnected && recentTimeSinceStartup - lastAliveTime >= 0.2f) {
						byte[] currentTime = BitConverter.GetBytes(recentTimeSinceStartup);
						byte[] message = new byte[5];
						message[0] = (byte)OpCode.StillAlive;
						Array.Copy(currentTime, 0, message, 1, 4);

						SendBytesRaw(message, false, true);
						SendBytesRaw(message, false, true, false, true);

						sentAlive10Seconds++;
						lastAliveTime = recentTimeSinceStartup;
					}

#if VALVESOCKETS_SPAN
					if (client != null) client.ReceiveMessagesOnConnection(connection, messageCallback, 256);
					if (fileClient != null) fileClient.ReceiveMessagesOnConnection(fileConnection, messageCallback, 256);
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

		private void ProcessMessage(byte[] buffer, uint inboundConnection) {
			Stopwatch messageTime = new Stopwatch();
			messageTime.Restart();
			OpCode opCode = (OpCode)buffer[0];
			byte playerID = buffer[buffer.Length - 1];

			if (opCode != OpCode.Animation && opCode != OpCode.StillAlive && opCode != OpCode.Sound)
				this.debugWriter.WriteLine("Received message with opcode {0}, and length {1}", opCode, buffer.Length);

			switch (opCode) {
				case OpCode.Connect:
					AddPlayer(playerID);
					break;
				case OpCode.Disconnect:
					MultiplayerRemotePlayerController player = remoteControllers.Find(c => c.playerID == playerID);
					chatMessages.Add("Player <color=\"yellow\">" + player.username + "{" + player.playerID + "}</color> <b><color=\"red\">DISCONNECTED</color></b>");
					if(player.replayAnimationFrames.Count > 5) {
						player.playerID = 255;
						player.skater.SetActive(false);
						player.board.SetActive(false);
						player.usernameObject.SetActive(false);
						if (column2Usernames.Count > 0) column2Usernames.RemoveAt(0);
						else if (column1Usernames.Count > 0) column1Usernames.RemoveAt(0);
						else if (column3Usernames.Count > 0) column3Usernames.RemoveAt(0);
					} else {
						RemovePlayer(player);
					}
					break;
				case OpCode.VersionNumber:
					serverConnectionNumber = BitConverter.ToUInt32(buffer, 1);
					string serverVersion = ASCIIEncoding.ASCII.GetString(buffer, 5, buffer.Length - 5);

					if (serverVersion.Equals(Main.modEntry.Version.ToString())) {
						this.debugWriter.WriteLine("Server version matches client version start encoding");

						if (fileClient == null) {
							fileClient = new NetworkingSockets();

							Address remoteAddress = new Address();
							remoteAddress.SetAddress(serverIPString, (ushort)(serverPort + 1));

							fileConnection = fileClient.Connect(ref remoteAddress);

							usernameThread = new Thread(SendUsername);
							usernameThread.IsBackground = true;
							usernameThread.Start();

							sendingUpdates = true;
						}
					} else {
						this.debugWriter.WriteLine("server version {0} does not match client version {1}", serverVersion, Main.modEntry.Version.ToString());
						Main.utilityMenu.SendImportantChat("<color=\"yellow\">server version <color=\"green\"><b>" + serverVersion + "</b></color> does not match client version <color=\"red\"><b>" + Main.modEntry.Version.ToString() + "</b></color></color>", 7500);
						DisconnectFromServer();
					}
					break;
				case OpCode.Settings:
					MultiplayerRemotePlayerController remotePlayer = remoteControllers.Find((p) => { return p.playerID == playerID; });
					if (remotePlayer != null) {
						remotePlayer.username = ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 2);
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
						break;
					}

					byte[] newBuffer = new byte[buffer.Length - 2];

					if (newBuffer.Length != 0) {
						Array.Copy(buffer, 1, newBuffer, 0, newBuffer.Length);
					}

					remoteOwner.ParseTextureStream(newBuffer);
					break;
				case OpCode.Animation:
					CompressedAnimations.Enqueue(Tuple.Create(playerID, buffer));
					break;
				case OpCode.Sound:
					CompressedSounds.Enqueue(Tuple.Create(playerID, buffer));
					break;
				case OpCode.Chat:
					MultiplayerRemotePlayerController remoteSender = this.remoteControllers.Find(p => p.playerID == playerID);
					string cleanedMessage = RemoveMarkup(ASCIIEncoding.ASCII.GetString(buffer, 1, buffer.Length - 2));

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
				case OpCode.ServerMessage:
					int duration = BitConverter.ToInt32(buffer, 1);
					string messageText = ASCIIEncoding.ASCII.GetString(buffer, 5, buffer.Length - 5);
					Main.utilityMenu.SendImportantChat(messageText, duration);
					break;
				case OpCode.StillAlive:
					if (inboundConnection == connection) {
						float sentTime = BitConverter.ToSingle(buffer, 1);
						pingTimes.Add(Time.realtimeSinceStartup - sentTime);
						receivedAlive10Seconds++;
					}
					break;
				case OpCode.PluginHash:
					byte pluginID = buffer[1];
					string hash = ASCIIEncoding.ASCII.GetString(buffer, 2, buffer.Length - 2);

					bool hasPlugin = false;
					Plugin localPlugin = null;
					foreach (Plugin plugin in Main.pluginList) {
						if (plugin.hash == hash) {
							hasPlugin = true;
							localPlugin = plugin;
							break;
						}
					}

					if (localPlugin != null)
						UnityModManagerNet.UnityModManager.Logger.Log($"Plugin {localPlugin.dllName} has id {pluginID}");

					if (hasPlugin) {
						EnablePlugin(localPlugin, pluginID);
					} else {
						byte[] PluginMissingMessage = new byte[buffer.Length];
						PluginMissingMessage[0] = (byte)OpCode.PluginHash;
						PluginMissingMessage[1] = 0;
						Array.Copy(buffer, 2, PluginMissingMessage, 2, buffer.Length - 2);

						client.SendMessageToConnection(connection, PluginMissingMessage, SendFlags.Reliable);
					}

					break;
				case OpCode.PluginFile:
					string[] oldPluginList = Main.pluginList.Select(p => p.hash).ToArray();

					int fileNameLength = BitConverter.ToInt32(buffer, 2);
					string fileName = ASCIIEncoding.ASCII.GetString(buffer, 6, fileNameLength);
					int fileLength = BitConverter.ToInt32(buffer, 6 + fileNameLength);
					byte[] fileContents = new byte[fileLength];

					Array.Copy(buffer, 10 + fileNameLength, fileContents, 0, fileLength);

					File.WriteAllBytes(Path.Combine(Main.modEntry.Path, "Plugins", fileName), fileContents);

					Main.LoadPlugins();

					byte newPluginID = buffer[1];
					Plugin newPlugin = null;
					foreach (Plugin p in Main.pluginList) {
						if (!oldPluginList.Where(s => s == p.hash).Any()) {
							newPlugin = p;
							break;
						}
					}

					Traverse.Create(newPlugin).Field("pluginID").SetValue(newPluginID);

					EnablePlugin(newPlugin, newPluginID);

					UnityModManagerNet.UnityModManager.Logger.Log($"New plugin {newPlugin.dllName} has id {newPluginID}");

					byte[] hashBytes = ASCIIEncoding.ASCII.GetBytes(newPlugin.hash);
					byte[] EnablePluginMessage = new byte[hashBytes.Length + 2];
					EnablePluginMessage[0] = (byte)OpCode.PluginHash;
					EnablePluginMessage[1] = 1;
					Array.Copy(hashBytes, 0, EnablePluginMessage, 2, hashBytes.Length);

					client.SendMessageToConnection(connection, EnablePluginMessage, SendFlags.Reliable);
					break;
				case OpCode.Plugin:
					byte destinationPlugin = buffer[1];
					byte[] pluginMessage = new byte[buffer.Length - 2];
					Array.Copy(buffer, 2, pluginMessage, 0, pluginMessage.Length);

					foreach (Plugin p in Main.pluginList) {
						if (p.pluginID == destinationPlugin) {
							p.ProcessMessage?.Invoke(pluginMessage);
							break;
						}
					}
					break;
			}

			messageTime.Stop();
			proccessedMessages.Add(Tuple.Create(messageTime.Elapsed.TotalMilliseconds, opCode));
		}

		private void EnablePlugin(Plugin localPlugin, byte pluginID) {
			Traverse.Create(localPlugin).Property("pluginID").SetValue(pluginID);
			localPlugin.TogglePlugin(true);

			byte[] hashBytes = ASCIIEncoding.ASCII.GetBytes(localPlugin.hash);
			byte[] PluginEnabledMessage = new byte[hashBytes.Length + 2];
			PluginEnabledMessage[0] = (byte)OpCode.PluginHash;
			PluginEnabledMessage[1] = 1;
			Array.Copy(hashBytes, 0, PluginEnabledMessage, 2, hashBytes.Length);

			client.SendMessageToConnection(connection, PluginEnabledMessage, SendFlags.Reliable);
		}

		private void DecompressSoundAnimationQueue() {
			while (playerController != null) {
				SpinWait.SpinUntil(() => !CompressedSounds.IsEmpty || !CompressedAnimations.IsEmpty || playerController == null);

				if (playerController == null) break;

				Tuple<byte, byte[]> CurrentSound;
				if (CompressedSounds.TryDequeue(out CurrentSound)) {
					byte[] soundData = Decompress(CurrentSound.Item2, 1, CurrentSound.Item2.Length - 2);

					DecompressedSounds.Enqueue(Tuple.Create(CurrentSound.Item1, soundData));
				}

				Tuple<byte, byte[]> CurrentAnimation;
				if (CompressedAnimations.TryDequeue(out CurrentAnimation)) {
					byte[] animationData = Decompress(CurrentAnimation.Item2, 5, CurrentAnimation.Item2.Length - 6, CurrentAnimation.Item2, 1, 4);
						
					DecompressedAnimations.Enqueue(Tuple.Create(CurrentAnimation.Item1, animationData));
				}
			}
		}

		private void ProcessSoundAnimationQueue() {
			Tuple<byte, byte[]> CurrentAnimation;
			while (!DecompressedAnimations.IsEmpty && DecompressedAnimations.TryDequeue(out CurrentAnimation)) {
				byte playerID = CurrentAnimation.Item1;

				MultiplayerRemotePlayerController targetPlayer = this.remoteControllers.Find(p => p.playerID == playerID);

				if(targetPlayer != null) {
					targetPlayer.UnpackAnimations(CurrentAnimation.Item2);
				}
			}
			
			Tuple<byte, byte[]> CurrentSound;
			while (!DecompressedSounds.IsEmpty && DecompressedSounds.TryDequeue(out CurrentSound)) {
				byte playerID = CurrentSound.Item1;

				MultiplayerRemotePlayerController targetPlayer = this.remoteControllers.Find(p => p.playerID == playerID);

				if (targetPlayer != null) {
					targetPlayer.UnpackSounds(CurrentSound.Item2);
				}
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
			if (this.playerController == null) return;

			Tuple<byte[], bool> animationData = this.playerController.PackAnimations();

			byte[] soundData = this.playerController.PackSounds();

			if (soundData != null) SendBytesRaw(soundData, true);

			if (animationData == null) return;

			// Only send update if it's new transforms(don't waste bandwidth on duplicates)
			if (this.playerController.currentAnimationTime != previousSentAnimationTime || 
				GameManagement.GameStateMachine.Instance.CurrentState.GetType() != typeof(GameManagement.PlayState)) {

				SendBytesAnimation(animationData.Item1, animationData.Item2);
				previousSentAnimationTime = this.playerController.currentAnimationTime;
			}
		}

		// For things that encode the prebuffer during serialization
		public void SendBytesRaw(byte[] msg, bool reliable, bool nonagle = false, bool nodelay = false, bool sendFileServer = false) {
			SendFlags sendType = reliable ? SendFlags.Reliable : SendFlags.Unreliable;
			if (nonagle) sendType |= SendFlags.NoNagle;
			if (nodelay) sendType |= SendFlags.NoDelay;
			if (client != null && !sendFileServer) client.SendMessageToConnection(connection, msg, sendType);
			else if (fileClient != null && sendFileServer) fileClient.SendMessageToConnection(fileConnection, msg, sendType);
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

				var client = new HttpClient();

				var values = new Dictionary<string, string> {
				{ "session", session } };
				
				// Try to give a session key instead of tying encryption key to IP(Useful if client has multiple IPs or something)
				
				var sendContent = new FormUrlEncodedContent(values);
				var response = client.PostAsync("http://davisellwood-site.herokuapp.com/api/gettempkey/", sendContent);
				response.Wait();

				while (!response.IsCompleted) ;
				
				string content = "";

				if (response.Result.StatusCode == HttpStatusCode.OK) {
					var stringResponse = response.Result.Content.ReadAsStringAsync();
					stringResponse.Wait();
					while (!stringResponse.IsCompleted) ;
					content = stringResponse.Result;
				}else {
					problemDetectedLocally = true;
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
				if (!afterMarkup.StartsWith("/")) chatMessages.Add(this.playerController.username + "<b><color=\"blue\"> (You): </color></b>" + afterMarkup);
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
			using (DeflateStream dstream = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal)) {
				dstream.Write(data, 0, data.Length);
			}
			return output.ToArray();
		}

		public static byte[] Decompress(byte[] data, int startIndex = 0, int decompressLength = -1, byte[] header = null, int headerStart = 0, int headerLength = 0) {
			if (decompressLength == -1) {
				decompressLength = data.Length;
			}
			MemoryStream output = new MemoryStream();
			if (header != null) {
				output.Write(header, headerStart, headerLength);
			}
			using (MemoryStream compressedStream = new MemoryStream(data, startIndex, decompressLength)) {
				using (DeflateStream dstream = new DeflateStream(compressedStream, CompressionMode.Decompress)) {
					dstream.CopyTo(output);
				}
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
			Main.OnClickDisconnect();
		}

		public void OnApplicationFocus(bool focused) {
			if (MultiplayerUtils.hashedMaps != LevelManager.Instance.CustomLevels.Count)
				MultiplayerUtils.StartMapLoading();
		}

		public void OnDestroy() {
			isConnected = false;
			sendingUpdates = false;

			Main.utilityMenu.isLoading = false;

			this.playerController = null;

			foreach (Plugin plugin in Main.pluginList) {
				plugin.TogglePlugin(false);
			}

			if (usernameThread != null && usernameThread.IsAlive) usernameThread.Join();
			if (networkMessageThread != null && networkMessageThread.IsAlive) networkMessageThread.Join();
			if (decompressionThread != null && decompressionThread.IsAlive) decompressionThread.Join();

			if (client != null) client.CloseConnection(connection);
			if (fileClient != null) fileClient.CloseConnection(fileConnection);

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

			this.debugWriter.Close();
			this.debugWriter.Dispose();
		}
	}
}
