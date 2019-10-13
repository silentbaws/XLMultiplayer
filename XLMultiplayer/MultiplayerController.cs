using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Text;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using Harmony12;
using RootMotion.FinalIK;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections;

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

	public class MultiplayerSkinBuffer {
		public byte[] buffer;
		public int connectionId;
		public MPTextureType textureType;
		private Stopwatch timer;

		public MultiplayerSkinBuffer(byte[] buffer, int connectionId, MPTextureType textureType) {
			this.buffer = buffer;
			this.connectionId = connectionId;
			this.textureType = textureType;
			timer = new Stopwatch();
		}

		public long ElapsedTime() {
			return timer.ElapsedMilliseconds;
		}
	}

	public class MultiplayerController : MonoBehaviour {
		public bool runningClient = false;

		public static byte tickRate = 30;

		public MultiplayerPlayerController ourController;
		public List<MultiplayerPlayerController> otherControllers = new List<MultiplayerPlayerController>();

		public StreamWriter debugWriter;

		public NetworkClient client;
		private Stopwatch textureSendWatch;

		public List<MultiplayerSkinBuffer> textureQueue = new List<MultiplayerSkinBuffer>();

		public Thread aliveThread;

		public bool isConnected = false;
		List<float> previousFrameTimes = new List<float> ();

		public static List<string> chatMessages = new List<string>();

		private void Start() {
		}

		private void Update() {
			if(GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.LevelSelectionState) && MultiplayerUtils.serverMapDictionary.Count > 0 && isConnected) {
				GameManagement.GameStateMachine.Instance.RequestPauseState();
			}

			if(previousFrameTimes.Count == 0)
				for(int i = 0; i < 1000; i++)
					previousFrameTimes.Add(10000);
			else {
				previousFrameTimes.RemoveAt(0);
				previousFrameTimes.Add(Time.unscaledDeltaTime);
			}

			this.UpdateClient();

			if(client != null && !this.isConnected && client.elapsedTime.ElapsedMilliseconds > 5000 + previousFrameTimes.Max()) {
				debugWriter.WriteLine("Failed to connect to server");
				KillConnection();
			}

			if (client == null) return;

			if (client.elapsedTime.ElapsedMilliseconds - client.lastAlive > 5000 + previousFrameTimes.Max() && ((IsInvoking("SendUpdate") && textureQueue.Count == 0) || !client.tcpConnection.Connected)) {
				bool loadedAll = true;
				foreach (MultiplayerPlayerController controller in this.otherControllers) {
					if (!controller.shirtMP.loaded) {
						loadedAll = false;
						break;
					}
					if (!controller.pantsMP.loaded) {
						loadedAll = false;
						break;
					}
					if (!controller.shoesMP.loaded) {
						loadedAll = false;
						break;
					}
					if (!controller.boardMP.loaded) {
						loadedAll = false;
						break;
					}
					if (!controller.hatMP.loaded) {
						loadedAll = false;
						break;
					}
				}
				if (loadedAll || this.otherControllers.Count == 0) client.timedOut = true;
			}

			//Use new frame buffer
			foreach (MultiplayerPlayerController controller in this.otherControllers) {
				if(controller != null) {
					controller.LerpNextFrame();
				}
			}
		}

		public void SendUpdate() {
			this.SendPlayerPosition();
			this.SendPlayerAnimator();
		}

		public void ConnectToServer(string serverIP, int port, string user) {
			if (!this.runningClient) {
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
				this.debugWriter.WriteLine("Attempting to connect to server ip {0} on port {1}", serverIP, port.ToString());

				this.ourController = new MultiplayerPlayerController(debugWriter);
				this.ourController.ConstructForPlayer();
				this.ourController.username = user;
				this.runningClient = true;

				MultiplayerUtils.serverMapDictionary.Clear();

				client = new NetworkClient(serverIP, port, this, this.debugWriter);

				FullBodyBipedIK biped = Traverse.Create(PlayerController.Instance.ikController).Field("_finalIk").GetValue<FullBodyBipedIK>();
				debugWriter.WriteLine(biped.references.pelvis.name);
				Transform parent = biped.references.root.parent;
				while (parent != null) {
					debugWriter.WriteLine(parent.name);
					parent = parent.parent;
				}
			}
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
			if(target == null) {
				target = levelSelectionController.CustomLevels.Find(level => level.path.Equals(path));
			}
			if(target == null) {
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
			yield break;
		}

		private void UpdateClient() {
			if (client != null) {
				byte[] buffer;
				int bufSize;
				bool gotObject = client.GetMessage(out bufSize, out buffer);
				while (gotObject) {
					this.ProcessMessage(buffer, bufSize);
					gotObject = client.GetMessage(out bufSize, out buffer);
				}
			}

			foreach (MultiplayerSkinBuffer bufferedSkin in textureQueue) {
				foreach (MultiplayerPlayerController player in otherControllers) {
					if (player.playerID == bufferedSkin.connectionId) {
						switch (bufferedSkin.textureType) {
							case MPTextureType.Pants:
								player.pantsMP.SaveTexture(bufferedSkin.connectionId, bufferedSkin.buffer);
								break;
							case MPTextureType.Shirt:
								player.shirtMP.SaveTexture(bufferedSkin.connectionId, bufferedSkin.buffer);
								break;
							case MPTextureType.Shoes:
								player.shoesMP.SaveTexture(bufferedSkin.connectionId, bufferedSkin.buffer);
								break;
							case MPTextureType.Board:
								player.boardMP.SaveTexture(bufferedSkin.connectionId, bufferedSkin.buffer);
								break;
							case MPTextureType.Hat:
								player.hatMP.SaveTexture(bufferedSkin.connectionId, bufferedSkin.buffer);
								break;
						}

						textureQueue.Remove(bufferedSkin);
						debugWriter.WriteLine("Removed texture from queue");
					}
				}

				if (bufferedSkin.ElapsedTime() > 600000) {
					textureQueue.Remove(bufferedSkin);
					debugWriter.WriteLine("Texture in queue expired");
				}
			}

			foreach (MultiplayerPlayerController player in otherControllers) {
				if (player.pantsMP.loaded == false && player.pantsMP.saved)
					player.pantsMP.LoadFromFileMainThread(player);
				if (player.shirtMP.loaded == false && player.shirtMP.saved)
					player.shirtMP.LoadFromFileMainThread(player);
				if (player.shoesMP.loaded == false && player.shoesMP.saved)
					player.shoesMP.LoadFromFileMainThread(player);
				if (player.boardMP.loaded == false && player.boardMP.saved)
					player.boardMP.LoadFromFileMainThread(player);
				if (player.hatMP.loaded == false && player.hatMP.saved)
					player.hatMP.LoadFromFileMainThread(player);
			}

			if (client != null && client.timedOut) {
				debugWriter.WriteLine("Client timed out");
				KillConnection();
			}
		}

		public void SendTextures() {
			while (!client.tcpConnection.Connected || !this.ourController.pantsMP.saved || !this.ourController.shirtMP.saved || !this.ourController.shoesMP.saved || !this.ourController.boardMP.saved || !this.ourController.hatMP.saved) {
				if (textureSendWatch == null) {
					textureSendWatch = new Stopwatch();
					textureSendWatch.Start();
				}

				if (textureSendWatch.ElapsedMilliseconds > 5000) {
					debugWriter.WriteLine("Connection to file server timed out or textures failed saving");
					KillConnection();
					return;
				}
			}

			//TODO: Add check hash of shirt/hoodie texture to determine if it's default

			string path = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\";

			byte[] prebuffer = new byte[15];
			Array.Copy(BitConverter.GetBytes(this.ourController.pantsMP.bytes.Length + prebuffer.Length - 4), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Pants;
			Array.Copy(BitConverter.GetBytes(this.ourController.pantsMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.pantsMP.size.y), 0, prebuffer, 10, 4);
			prebuffer[14] = 0;
			client.tcpConnection.SendFile(path + "Pants.jpg", prebuffer, null, TransmitFileOptions.UseSystemThread);

			prebuffer = new byte[15];
			Array.Copy(BitConverter.GetBytes(this.ourController.shirtMP.bytes.Length + prebuffer.Length - 4), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Shirt;
			Array.Copy(BitConverter.GetBytes(this.ourController.shirtMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.shirtMP.size.y), 0, prebuffer, 10, 4);
			bool useHoodie = false;
			foreach(Tuple<CharacterGear, GameObject> tup in this.ourController.gearList) {
				if(tup.Item1.categoryName.Equals("Hoodie") || tup.Item1.categoryName.Equals("Shirt")) {
					useHoodie = tup.Item1.categoryName.Equals("Hoodie");
					break;
				}
			}
			prebuffer[14] = useHoodie ? (byte)1 : (byte)0;
			client.tcpConnection.SendFile(path + "Shirt.jpg", prebuffer, null, TransmitFileOptions.UseSystemThread);

			prebuffer = new byte[15];
			Array.Copy(BitConverter.GetBytes(this.ourController.shoesMP.bytes.Length + prebuffer.Length - 4), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Shoes;
			Array.Copy(BitConverter.GetBytes(this.ourController.shoesMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.shoesMP.size.y), 0, prebuffer, 10, 4);
			prebuffer[14] = 0;
			client.tcpConnection.SendFile(path + "Shoes.jpg", prebuffer, null, TransmitFileOptions.UseSystemThread);

			prebuffer = new byte[15];
			Array.Copy(BitConverter.GetBytes(this.ourController.boardMP.bytes.Length + prebuffer.Length - 4), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Board;
			Array.Copy(BitConverter.GetBytes(this.ourController.boardMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.boardMP.size.y), 0, prebuffer, 10, 4);
			prebuffer[14] = 0;
			client.tcpConnection.SendFile(path + "Board.jpg", prebuffer, null, TransmitFileOptions.UseSystemThread);

			prebuffer = new byte[15];
			Array.Copy(BitConverter.GetBytes(this.ourController.hatMP.bytes.Length + prebuffer.Length - 4), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Hat;
			Array.Copy(BitConverter.GetBytes(this.ourController.hatMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.hatMP.size.y), 0, prebuffer, 10, 4);
			prebuffer[14] = 0;
			client.tcpConnection.SendFile(path + "Hat.jpg", prebuffer, null, TransmitFileOptions.UseSystemThread);
		}

		private void AddPlayer(byte playerID) {
			MultiplayerPlayerController newController = new MultiplayerPlayerController(debugWriter);
			newController.ConstructFromPlayer(this.ourController);
			newController.playerID = playerID;
			otherControllers.Add(newController);
		}

		private void RemovePlayer(int playerID) {
			int index = -1;
			for (int i = 0; i < otherControllers.Count; i++) {
				if (otherControllers[i].playerID == playerID) {
					index = i;
					break;
				}
			}
			if (index != -1) {
				MultiplayerPlayerController controller = otherControllers[index];
				otherControllers.RemoveAt(index);
				Destroy(controller.player);
			}
		}

		private void SendPlayerPosition() {
			this.SendBytes(OpCode.Position, this.ourController.PackTransforms(), false);
		}

		private void SendPlayerAnimator() {
			byte[] packed = this.ourController.PackAnimator();
			this.SendBytes(OpCode.Animation, packed, false);
		}

		public void KillConnection() {
			if (IsInvoking("SendUpdate"))
				CancelInvoke("SendUpdate");
			MultiplayerController.chatMessages.Clear();
			if (MultiplayerUtils.serverMapDictionary != null) {
				MultiplayerUtils.serverMapDictionary.Clear();
			}
			if(Main.statusMenu != null) {
				Main.statusMenu.previousMessageCount = 0;
				Main.statusMenu.chat = "";
			}
			this.isConnected = false;
			if (aliveThread != null && aliveThread.IsAlive)
				aliveThread.Abort();
			if (client != null) {
				if(client.tcpConnection != null && client.tcpConnection.Connected)
					client.tcpConnection.Disconnect(false);
			}
			this.textureSendWatch = null;
			if (otherControllers.Count > 0) {
				List<int> players = new List<int>();
				foreach (MultiplayerPlayerController connection in otherControllers) {
					players.Add(connection.playerID);
				}
				foreach (int i in players) {
					RemovePlayer(i);
				}
			}
			otherControllers.Clear();
			string path = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing";
			if (Directory.Exists(path)) {
				string[] files = Directory.GetFiles(path);
				foreach (string file in files) {
					File.Delete(file);
				}
				Directory.Delete(path);
			}
			path = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp";
			if (Directory.Exists(path)) {
				string[] files = Directory.GetFiles(path);
				foreach (string file in files) {
					File.Delete(file);
				}
				Directory.Delete(path);
			}
			this.client = null;
			this.runningClient = false;
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}

		public void ProcessMessage(byte[] buffer, int bufferSize) {
			byte[] newBuffer = new byte[bufferSize - 2];
			Array.Copy(buffer, 1, newBuffer, 0, bufferSize - 2);

			OpCode opCode = (OpCode)buffer[0];
			byte playerID = buffer[bufferSize - 1];

			switch (opCode) {
				case OpCode.Position:
					foreach (MultiplayerPlayerController controller in otherControllers) {
						if (controller.playerID == playerID)
							controller.UnpackTransforms(newBuffer);
					}
					break;
				case OpCode.Animation:
					foreach (MultiplayerPlayerController controller in otherControllers) {
						if (controller.playerID == playerID)
							controller.UnpackAnimator(newBuffer);
					}
					break;
				case OpCode.Settings:
					debugWriter.WriteLine("Processing settings from {0}", playerID);
					foreach (MultiplayerPlayerController controller in otherControllers) {
						if (controller.playerID == playerID) {
							controller.username = ASCIIEncoding.ASCII.GetString(newBuffer, 1, newBuffer.Length - 1);
							debugWriter.WriteLine(controller.username);
							MultiplayerController.chatMessages.Add("Player <color=\"yellow\">" + controller.username + "{" + controller.playerID + "}</color> <b><color=\"green\">CONNECTED</color></b>");
						}
					}
					break;
				case OpCode.Chat:
					string user = "";
					foreach(MultiplayerPlayerController controller in otherControllers) {
						if (controller.playerID == playerID) {
							user = controller.username;
						}
					}
					string msg = ASCIIEncoding.ASCII.GetString(newBuffer);
					msg = RemoveMarkup(msg);
					string message = "<b>" + user + "</b>{" + playerID.ToString() + "}: " + msg;
					MultiplayerController.chatMessages.Add(message);
					break;
				case OpCode.Connect:
					if (buffer.Length == 2) {
						debugWriter.WriteLine("New player {0}", playerID);
						this.AddPlayer(playerID);
					} else {
						debugWriter.WriteLine("Successfully connected to server");
						byte[] usernameBytes = Encoding.ASCII.GetBytes(this.ourController.username);
						byte[] settingsBytes = new byte[usernameBytes.Length + 1];
						settingsBytes[0] = SettingsManager.Instance.stance == SettingsManager.Stance.Goofy ? (byte)1 : (byte)0;
						Array.Copy(usernameBytes, 0, settingsBytes, 1, usernameBytes.Length);
						this.SendBytes(OpCode.Settings, settingsBytes, true);
						this.isConnected = true;
						aliveThread = new Thread(new ThreadStart(this.SendAlive));
						aliveThread.IsBackground = true;
						aliveThread.Start();
						StartCoroutine(this.ourController.EncodeTextures());
						break;
					}
					break;
				case OpCode.Disconnect:
					if (buffer.Length == 2) {
						foreach (MultiplayerPlayerController controller in otherControllers) {
							if (controller.playerID == playerID) {
								MultiplayerController.chatMessages.Add("Player <color=\"yellow\">" + controller.username + "{" + controller.playerID + "}</color> <b><color=\"red\">DISCONNECTED</color></b>");
							}
						}
						this.RemovePlayer(playerID);
					} else {
						string versionNumber = ASCIIEncoding.ASCII.GetString(newBuffer);
						Main.statusMenu.DisplayInvalidVersion(versionNumber);
						KillConnection();
					}
					break;
				case OpCode.StillAlive:
					long timeOfPacket = BitConverter.ToInt64(buffer, 1);
					client.ping = (int)(client.elapsedTime.ElapsedMilliseconds - timeOfPacket - Time.unscaledDeltaTime);

					client.lastAlive = client.elapsedTime.ElapsedMilliseconds > client.lastAlive ? client.elapsedTime.ElapsedMilliseconds : client.lastAlive;

					client.receivedAlive++;
					client.packetLoss = Mathf.Clamp(((1.0f - (float)client.receivedAlive / (float)client.sentAlive) * 100), 0.0f, 99.9f);

					//debugWriter.WriteLine("Current ping {0}ms, packet loss {1}%", client.ping, client.packetLoss.ToString("n2"));
					break;
			}
		}

		public void OnDestroy() {
			KillConnection();
		}

		public void OnApplicationQuit() {
			KillConnection();
		}

		private void SendAlive() {
			while (this.isConnected) {
				if (client != null) {
					client.SendAlive();
				}
				Thread.Sleep(100);
			}
		}

		public void SendChatMessage(string message) {
			if (!message.Equals("")) {
				byte[] msg = ASCIIEncoding.ASCII.GetBytes(message);
				this.SendBytes(OpCode.Chat, msg, true);
				MultiplayerController.chatMessages.Add("<b><color=\"blue\">You: </color></b>" + RemoveMarkup(message));
			}
		}

		public void SendBytes(OpCode opCode, byte[] msg, bool reliable) {
			if (!reliable) {
				client.SendUnreliable(msg, opCode);
			}else if(reliable && client.tcpConnection.Connected) {
				byte[] buffer = new byte[msg.Length + 5];
				Array.Copy(BitConverter.GetBytes(msg.Length + 1), 0, buffer, 0, 4);
				buffer[4] = (byte)opCode;
				Array.Copy(msg, 0, buffer, 5, msg.Length);

				client.SendReliable(buffer);
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
	}
}