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

namespace XLMultiplayer {
	public enum OpCode : byte{
		Connect = 0,
		Settings = 1,
		Position = 2,
		Animation = 3,
		Texture = 4,
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

		private byte tickRate = 32;

		public MultiplayerPlayerController ourController;
		public List<MultiplayerPlayerController> otherControllers = new List<MultiplayerPlayerController>();

		public StreamWriter debugWriter;

		public NetworkClient client;
		private Stopwatch textureSendWatch;

		public List<MultiplayerSkinBuffer> textureQueue = new List<MultiplayerSkinBuffer>();

		public Thread aliveThread;

		public bool isConnected = false;

		public static RuntimeAnimatorController goofyAnim = Traverse.Create(SettingsManager.Instance).Field("_goofyAnim").GetValue<RuntimeAnimatorController>();
		public static RuntimeAnimatorController regularAnim = Traverse.Create(SettingsManager.Instance).Field("_regularAnim").GetValue<RuntimeAnimatorController>();
		public static RuntimeAnimatorController goofySteezeAnim = Traverse.Create(SettingsManager.Instance).Field("_goofySteezeAnim").GetValue<RuntimeAnimatorController>();
		public static RuntimeAnimatorController regularSteezeAnim = Traverse.Create(SettingsManager.Instance).Field("_regularSteezeAnim").GetValue<RuntimeAnimatorController>();

		private void Start() {
		}

		private void Update() {
			this.UpdateClient();

			if(client != null && !client.tcpConnection.Connected && client.elapsedTime.ElapsedMilliseconds > 5000) {
				KillConnection();
			}
		}

		private void SendUpdate() {
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

				client = new NetworkClient(serverIP, port, this);
				client.debugWriter = debugWriter;

				//FullBodyBipedIK biped = Traverse.Create(PlayerController.Instance.ikController).Field("_finalIk").GetValue<FullBodyBipedIK>();
				//debugWriter.WriteLine(biped.references.root.name);
				//Transform parent = biped.references.root.parent;
				//while (parent != null) {
				//	debugWriter.WriteLine(parent.name);
				//	parent = parent.parent;
				//}
			}
		}

		private void UpdateClient() {
			byte[] buffer;
			int bufSize;
			bool gotObject = client.GetMessage(out bufSize, out buffer);
			while (gotObject) {
				this.ProcessMessage(buffer, bufSize);
				gotObject = client.GetMessage(out bufSize, out buffer);
			}

			foreach(MultiplayerSkinBuffer bufferedSkin in textureQueue) {
				foreach(MultiplayerPlayerController player in otherControllers) {
					if(player.playerID == bufferedSkin.connectionId) {
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

				if(bufferedSkin.ElapsedTime() > 10000) {
					textureQueue.Remove(bufferedSkin);
					debugWriter.WriteLine("Texture in queue expired");
				}
			}

			foreach(MultiplayerPlayerController player in otherControllers) {
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
				KillConnection();
			}
		}

		private void SendTextures() {
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

			string path = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\";

			byte[] prebuffer = new byte[14];
			Array.Copy(BitConverter.GetBytes(this.ourController.pantsMP.bytes.Length + 10), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Pants;
			Array.Copy(BitConverter.GetBytes(this.ourController.pantsMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.pantsMP.size.y), 0, prebuffer, 10, 4);
			client.tcpConnection.SendFile(path + "Pants.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);

			prebuffer = new byte[14];
			Array.Copy(BitConverter.GetBytes(this.ourController.shirtMP.bytes.Length + 10), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Shirt;
			Array.Copy(BitConverter.GetBytes(this.ourController.shirtMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.shirtMP.size.y), 0, prebuffer, 10, 4);
			client.tcpConnection.SendFile(path + "Shirt.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);

			prebuffer = new byte[14];
			Array.Copy(BitConverter.GetBytes(this.ourController.shoesMP.bytes.Length + 10), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Shoes;
			Array.Copy(BitConverter.GetBytes(this.ourController.shoesMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.shoesMP.size.y), 0, prebuffer, 10, 4);
			client.tcpConnection.SendFile(path + "Shoes.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);

			prebuffer = new byte[14];
			Array.Copy(BitConverter.GetBytes(this.ourController.boardMP.bytes.Length + 10), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Board;
			Array.Copy(BitConverter.GetBytes(this.ourController.boardMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.boardMP.size.y), 0, prebuffer, 10, 4);
			client.tcpConnection.SendFile(path + "Board.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);

			prebuffer = new byte[14];
			Array.Copy(BitConverter.GetBytes(this.ourController.hatMP.bytes.Length + 10), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)OpCode.Texture;
			prebuffer[5] = (byte)MPTextureType.Hat;
			Array.Copy(BitConverter.GetBytes(this.ourController.hatMP.size.x), 0, prebuffer, 6, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.hatMP.size.y), 0, prebuffer, 10, 4);
			client.tcpConnection.SendFile(path + "Hat.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);
		}

		private void AddPlayer(int playerID) {
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
			this.SendBytes(OpCode.Animation, this.ourController.PackAnimator(), false);
		}

		public void KillConnection() {
			if (IsInvoking("SendUpdate"))
				CancelInvoke("SendUpdate");
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

		private void ProcessMessage(byte[] buffer, int bufferSize) {
			byte[] newBuffer = new byte[bufferSize - 5];
			Array.Copy(buffer, 1, newBuffer, 0, bufferSize - 5);

			OpCode opCode = (OpCode)buffer[0];
			int playerID = BitConverter.ToInt32(buffer, bufferSize - 4);

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
							controller.animator.runtimeAnimatorController = newBuffer[0] == 1 ? goofyAnim : regularAnim;
							controller.steezeAnimator.runtimeAnimatorController = newBuffer[0] == 1 ? goofySteezeAnim : regularSteezeAnim;
							controller.username = Encoding.ASCII.GetString(newBuffer, 1, newBuffer.Length - 1);
							debugWriter.WriteLine(controller.username);
						}
					}
					break;
				case OpCode.Connect:
					if (buffer.Length == 5) {
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
						aliveThread.Start();
						this.ourController.EncodeTextures();
						SendTextures();
						InvokeRepeating("SendUpdate", 0.5f, 1.0f / (float)tickRate);
						break;
					}
					break;
				case OpCode.Disconnect:
					if(buffer.Length == 5)
						this.RemovePlayer(playerID);
					else {
						KillConnection();
					}
					break;
				case OpCode.StillAlive:
					long timeOfPacket = BitConverter.ToInt64(buffer, 1);
					long ping = client.elapsedTime.ElapsedMilliseconds - timeOfPacket;

					client.lastAlive = client.elapsedTime.ElapsedMilliseconds;
					client.receivedAlive++;
					client.packetLoss = ((1.0f - (float)client.receivedAlive / (float)client.sentAlive) * 100);

					debugWriter.WriteLine("Current ping {0}ms, packet loss {1}%", ping, client.packetLoss);
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
					if (client.elapsedTime.ElapsedMilliseconds - client.lastAlive > 5000 && IsInvoking("SendUpdate")) {
						client.timedOut = true;
					}
				}
				Thread.Sleep(100);
			}
		}

		private void SendBytes(OpCode opCode, byte[] msg, bool reliable) {
			if (!reliable) {
				byte[] buffer = new byte[msg.Length + 1];
				buffer[0] = (byte)opCode;
				Array.Copy(msg, 0, buffer, 1, msg.Length);

				client.SendUnreliable(buffer);
			}else if(reliable && client.tcpConnection.Connected) {
				byte[] buffer = new byte[msg.Length + 5];
				Array.Copy(BitConverter.GetBytes(msg.Length + 1), 0, buffer, 0, 4);
				buffer[4] = (byte)opCode;
				Array.Copy(msg, 0, buffer, 5, msg.Length);

				client.SendReliable(buffer);
			}
		}
	}
}