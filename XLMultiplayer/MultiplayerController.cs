using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace XLMultiplayer {
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
		FileTransferClient fileTransfer;
		private string IP;
		private int PORT;
		private Stopwatch textureSendWatch;

		public List<MultiplayerSkinBuffer> textureQueue = new List<MultiplayerSkinBuffer>();

		private void Start() {
		}

		private void Update() {
			this.UpdateClient();
		}

		private void SendUpdate() {
			this.SendPlayerPosition();
			this.SendPlayerAnimator();
		}

		public void ConnectToServer(string serverIP, int port, string user) {
			if (!this.runningClient && !this.runningServer) {
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
				NetworkTransport.Init();
				ConnectionConfig connectionConfig = new ConnectionConfig();
				connectionConfig.PacketSize = 1400;
				connectionConfig.InitialBandwidth = 15360000;
				connectionConfig.SendDelay = 10;
				connectionConfig.ResendTimeout = 600;
				connectionConfig.MaxSentMessageQueueSize = 300;
				connectionConfig.AcksType = ConnectionAcksType.Acks128;
				this.reliableChannel = connectionConfig.AddChannel(QosType.Reliable);
				this.unreliableChannel = connectionConfig.AddChannel(QosType.UnreliableSequenced);
				this.reliableSequencedChannel = connectionConfig.AddChannel(QosType.ReliableSequenced);
				HostTopology topology = new HostTopology(connectionConfig, 1);
				this.hostId = NetworkTransport.AddHost(topology);
				if (this.hostId < 0) {
					this.debugWriter.WriteLine("Failed socket creation for client");
					NetworkTransport.Shutdown();
					return;
				} else {
					this.debugWriter.WriteLine("Successfully created client socket");
				}
				this.connectionId = NetworkTransport.Connect(this.hostId, serverIP, port, 0, out this.error);
				if (this.error != 0) {
					this.debugWriter.WriteLine((NetworkError)this.error);
					return;
				}

				this.ourController = new MultiplayerPlayerController(debugWriter);
				this.ourController.ConstructForPlayer();
				this.ourController.username = user;
				this.runningClient = true;

				this.IP = serverIP;
				this.PORT = port;
			}
		}

		private void UpdateClient() {
			byte[] buffer = new byte[1024];
			int hId;
			int conId;
			int chanId;
			int bufSize;
			NetworkEventType networkEvent = NetworkTransport.Receive(out hId, out conId, out chanId, buffer, 1024, out bufSize, out this.error);
			while (networkEvent != NetworkEventType.Nothing) {
				if (this.error != (int)NetworkError.Ok)
					debugWriter.WriteLine("Error recieving message {0}", this.error);
				switch (networkEvent) {
					case NetworkEventType.ConnectEvent:
						debugWriter.WriteLine("Successfully connected to server");
						this.SendBytes(2, Encoding.ASCII.GetBytes(this.ourController.username), this.reliableChannel);
						this.ourController.EncodeTextures();
						fileTransfer = new FileTransferClient(this.IP, this.PORT + 1, this);
						SendTextures();
						InvokeRepeating("SendUpdate", 0.5f, 1.0f / (float)tickRate);
						break;
					case NetworkEventType.DataEvent:
						ProcessMessage(buffer, bufSize);
						break;
					case NetworkEventType.DisconnectEvent:
						this.debugWriter.WriteLine("Connection to server ended");
						this.KillConnection();
						break;
				}
				networkEvent = NetworkTransport.Receive(out hId, out conId, out chanId, buffer, 1024, out bufSize, out this.error);
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
						debugWriter.WriteLine("Saved texture in queue");
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
		}

		private void SendTextures() {
			while (!fileTransfer.connection.Connected || !this.ourController.pantsMP.saved || !this.ourController.shirtMP.saved || !this.ourController.shoesMP.saved || !this.ourController.boardMP.saved || !this.ourController.hatMP.saved) {
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

			byte[] prebuffer = new byte[13];
			Array.Copy(BitConverter.GetBytes(this.ourController.pantsMP.bytes.Length + 9), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)MPTextureType.Pants;
			Array.Copy(BitConverter.GetBytes(this.ourController.pantsMP.size.x), 0, prebuffer, 5, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.pantsMP.size.y), 0, prebuffer, 9, 4);
			fileTransfer.connection.SendFile(path + "Pants.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);

			prebuffer = new byte[13];
			Array.Copy(BitConverter.GetBytes(this.ourController.shirtMP.bytes.Length + 9), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)MPTextureType.Shirt;
			Array.Copy(BitConverter.GetBytes(this.ourController.shirtMP.size.x), 0, prebuffer, 5, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.shirtMP.size.y), 0, prebuffer, 9, 4);
			fileTransfer.connection.SendFile(path + "Shirt.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);

			prebuffer = new byte[13];
			Array.Copy(BitConverter.GetBytes(this.ourController.shoesMP.bytes.Length + 9), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)MPTextureType.Shoes;
			Array.Copy(BitConverter.GetBytes(this.ourController.shoesMP.size.x), 0, prebuffer, 5, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.shoesMP.size.y), 0, prebuffer, 9, 4);
			fileTransfer.connection.SendFile(path + "Shoes.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);

			prebuffer = new byte[13];
			Array.Copy(BitConverter.GetBytes(this.ourController.boardMP.bytes.Length + 9), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)MPTextureType.Board;
			Array.Copy(BitConverter.GetBytes(this.ourController.boardMP.size.x), 0, prebuffer, 5, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.boardMP.size.y), 0, prebuffer, 9, 4);
			fileTransfer.connection.SendFile(path + "Board.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);

			prebuffer = new byte[13];
			Array.Copy(BitConverter.GetBytes(this.ourController.hatMP.bytes.Length + 9), 0, prebuffer, 0, 4);
			prebuffer[4] = (byte)MPTextureType.Hat;
			Array.Copy(BitConverter.GetBytes(this.ourController.hatMP.size.x), 0, prebuffer, 5, 4);
			Array.Copy(BitConverter.GetBytes(this.ourController.hatMP.size.y), 0, prebuffer, 9, 4);
			fileTransfer.connection.SendFile(path + "Hat.png", prebuffer, null, TransmitFileOptions.UseDefaultWorkerThread);
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
			this.SendBytes(0, this.ourController.PackTransforms(), this.unreliableChannel);
		}

		private void SendPlayerAnimator() {
			this.SendBytes(1, this.ourController.PackAnimator(), this.unreliableChannel);
		}

		public void KillConnection() {
			if (IsInvoking("SendUpdate"))
				CancelInvoke("SendUpdate");
			if (fileTransfer != null) {
				fileTransfer.CloseConnection();
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
			NetworkTransport.Disconnect(this.hostId, this.connectionId, out this.error);
			NetworkTransport.Shutdown();
			this.runningClient = false;
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}

		private void ProcessMessage(byte[] buffer, int bufferSize) {
			byte[] newBuffer = new byte[bufferSize - 5];
			Array.Copy(buffer, 1, newBuffer, 0, bufferSize - 5);

			byte opCode = buffer[0];
			int playerID = BitConverter.ToInt32(buffer, bufferSize - 4);

			this.debugWriter.WriteLine("Message: {0} {1}", opCode, playerID);

			switch (opCode) {
				case 0:
					foreach (MultiplayerPlayerController controller in otherControllers) {
						if (controller.playerID == playerID)
							controller.UnpackTransforms(newBuffer);
					}
					break;
				case 1:
					foreach (MultiplayerPlayerController controller in otherControllers) {
						if (controller.playerID == playerID)
							controller.UnpackAnimator(newBuffer);
					}
					break;
				case 2:
					foreach (MultiplayerPlayerController controller in otherControllers) {
						if (controller.playerID == playerID) {
							controller.username = Encoding.ASCII.GetString(newBuffer, 0, bufferSize - 5);
							debugWriter.WriteLine(controller.username);
						}
					}
					break;
				case 254:
					this.AddPlayer(playerID);
					break;
				case 255:
					this.RemovePlayer(playerID);
					break;
			}
		}

		public void OnDestroy() {
			KillConnection();
		}

		public void OnApplicationQuit() {
			KillConnection();
		}

		private void SendBytes(byte opCode, byte[] msg, byte channel) {
			if (this.connectionId != 0) {
				byte[] buffer = new byte[msg.Length + 1];
				buffer[0] = opCode;
				Array.Copy(msg, 0, buffer, 1, msg.Length);
				NetworkTransport.Send(this.hostId, this.connectionId, (int)channel, buffer, buffer.Length, out this.error);
				if (this.error != 0) {
					this.debugWriter.WriteLine((NetworkError)this.error);
				}
			}
		}

		public bool runningServer = false;
		public bool runningClient = false;

		private byte tickRate = 32;

		public MultiplayerPlayerController ourController;
		public List<MultiplayerPlayerController> otherControllers = new List<MultiplayerPlayerController>();

		private int hostId;
		private int connectionId;
		private byte unreliableChannel;
		private byte reliableChannel;
		private byte reliableSequencedChannel;
		private byte error;

		public StreamWriter debugWriter;
	}

	public class FileTransferClient {
		public class StateObject {
			public Socket workSocket = null;
			public byte[] buffer;
			public int readBytes = 0;
		}

		IPAddress ip;
		IPEndPoint ipEndPoint;

		public Socket connection;

		MultiplayerController controller;

		public FileTransferClient(string ipAdr, int port, MultiplayerController controller) {
			this.controller = controller;
			ip = IPAddress.Parse(ipAdr);
			ipEndPoint = new IPEndPoint(ip, port);

			connection = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			connection.BeginConnect(ipEndPoint, new AsyncCallback(ConnectCallback), connection);
		}

		private void ConnectCallback(IAsyncResult ar) {
			connection = (Socket)ar.AsyncState;
			connection.EndConnect(ar);
			BeginReceiving();
		}

		private void BeginReceiving() {
			StateObject state = new StateObject();
			state.workSocket = connection;
			state.buffer = new byte[4];
			state.readBytes = 0;
			connection.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None, ReceiveCallback, state);
		}

		public void ReceiveCallback(IAsyncResult ar) {
			try {
				StateObject state = (StateObject)ar.AsyncState;
				Socket handler = state.workSocket;
				int bytesRead = handler.EndReceive(ar);

				if (bytesRead > 0) {
					state.readBytes += bytesRead;
					if (state.readBytes < 4) {
						handler.BeginReceive(state.buffer, state.readBytes, state.buffer.Length - state.readBytes, SocketFlags.None, ReceiveCallback, state);
					} else {
						if (state.readBytes == 4) {
							controller.debugWriter.WriteLine("Getting shit");
							state.buffer = new byte[BitConverter.ToInt32(state.buffer, 0)];
						}

						if (state.readBytes - 4 == state.buffer.Length) {
							controller.debugWriter.WriteLine("Got shit");
							controller.debugWriter.WriteLine(state.buffer[0].ToString());

							controller.textureQueue.Add(new MultiplayerSkinBuffer(state.buffer, (int)state.buffer[0], (MPTextureType)state.buffer[1]));

							BeginReceiving();
						} else {
							handler.BeginReceive(state.buffer, state.readBytes - 4, state.buffer.Length - state.readBytes + 4, SocketFlags.None, ReceiveCallback, state);
						}
					}
				} else {
					CloseConnection();
				}
			} catch (Exception e) {
				if (connection.Connected) {
					BeginReceiving();
				} else {
					CloseConnection();
				}
			}
		}

		public void CloseConnection() {
			connection.Shutdown(SocketShutdown.Both);
			connection.Close();
		}
	}
}