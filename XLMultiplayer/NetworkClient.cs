using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace XLMultiplayer {
	public class NetworkClient {
		public class StateObject {
			public Socket workSocket = null;
			public UdpClient udpClient = null;
			public byte[] buffer;
			public int readBytes = 0;
		}

		public struct BufferObject {
			public byte[] buffer;
			public int bufSize;

			public BufferObject(byte[] buf, int size) {
				buffer = buf;
				bufSize = size;
			}
		}

		IPAddress ip;
		IPEndPoint ipEndPoint;

		public Socket tcpConnection;
		public UdpClient udpConnection;

		private List<BufferObject> bufferObjects = new List<BufferObject>();
		private int animationPackets = 0;
		private int positionPackets = 0;

		public Stopwatch elapsedTime;
		public bool timedOut = false;

		public int sentAlive = 0;
		public int receivedAlive = 0;
		public long lastAlive = 0;
		public float packetLoss;
		public int ping;

		public StreamWriter debugWriter;

		MultiplayerController controller;

		public NetworkClient(string ipAdr, int port, MultiplayerController controller) {
			this.controller = controller;
			ip = IPAddress.Parse(ipAdr);
			ipEndPoint = new IPEndPoint(ip, port);

			elapsedTime = new Stopwatch();
			elapsedTime.Start();

			tcpConnection = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			udpConnection = new UdpClient();
			udpConnection.Connect(ipEndPoint);

			tcpConnection.BeginConnect(ipEndPoint, new AsyncCallback(ConnectCallbackTCP), tcpConnection);
		}

		public bool GetMessage(out int bufferSize, out byte[] buffer) {
			if(bufferObjects.Count > 0) {
				bufferSize = bufferObjects[0].bufSize;
				buffer = bufferObjects[0].buffer;
				bufferObjects.RemoveAt(0);
				return true;
			} else {
				bufferSize = 0;
				buffer = null;
				return false;
			}
		}

		public void SendReliable(byte[] buffer) {
			try {
				tcpConnection.SendTo(buffer, ipEndPoint);
			} catch (Exception e) { }
		}

		public void SendAlive() {
			try {
				byte[] buffer = new byte[9];
				buffer[0] = (byte)OpCode.StillAlive;
				Array.Copy(BitConverter.GetBytes(elapsedTime.ElapsedMilliseconds), 0, buffer, 1, 8);

				if(sentAlive == 0) {
					lastAlive = elapsedTime.ElapsedMilliseconds;
				}
				sentAlive++;

				udpConnection.Send(buffer, buffer.Length);
			} catch (Exception e) { }
		}

		public void SendUnreliable(byte[] buffer) {
			try { 
				//Rearrange message before sending
				//Size of packet, opcode, packet sequence, rest of information
				byte[] packetSequence = BitConverter.GetBytes(buffer[0] == (byte)OpCode.Position ? positionPackets : animationPackets);
				byte[] packet = new byte[buffer.Length + packetSequence.Length];
				packet[0] = buffer[0];
				Array.Copy(packetSequence, 0, packet, 1, 4);
				Array.Copy(buffer, 1, packet, 5, buffer.Length - 1);

				udpConnection.Send(packet, packet.Length);

				if(buffer[0] == (byte)OpCode.Position) {
					positionPackets++;
				} else {
					animationPackets++;
				}
			} catch (Exception e) { }
		}

		private void BeginReceivingUDP() {
			StateObject state = new StateObject();
			state.udpClient = udpConnection;

			udpConnection.BeginReceive(ReceiveCallbackUDP, state);
		}

		public void ReceiveCallbackUDP(IAsyncResult ar) {
			try {
				StateObject state = (StateObject)ar.AsyncState;

				IPEndPoint tempEndPoint = ipEndPoint;

				byte[] buffer = state.udpClient.EndReceive(ar, ref tempEndPoint);
				
				bufferObjects.Add(new BufferObject(buffer, buffer.Length));

				BeginReceivingUDP();
			} catch (Exception e) {
				debugWriter.WriteLine(e.ToString());
				if (tcpConnection.Connected) {
					BeginReceivingUDP();
				} else {
					CloseConnection();
				}
			}
		}

		private void ConnectCallbackTCP(IAsyncResult ar) {
			tcpConnection = (Socket)ar.AsyncState;
			tcpConnection.EndConnect(ar);
			this.ipEndPoint = (IPEndPoint)tcpConnection.RemoteEndPoint;

			BeginReceivingTCP();
			BeginReceivingUDP();
		}

		private void BeginReceivingTCP() {
			StateObject state = new StateObject();
			state.workSocket = tcpConnection;
			state.buffer = new byte[4];
			state.readBytes = 0;
			tcpConnection.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None, ReceiveCallbackTCP, state);
		}

		public void ReceiveCallbackTCP(IAsyncResult ar) {
			try {
				StateObject state = (StateObject)ar.AsyncState;
				Socket handler = state.workSocket;
				int bytesRead = handler.EndReceive(ar);

				if (bytesRead > 0) {
					state.readBytes += bytesRead;
					if (state.readBytes < 4) {
						handler.BeginReceive(state.buffer, state.readBytes, state.buffer.Length - state.readBytes, SocketFlags.None, ReceiveCallbackTCP, state);
					} else {
						lastAlive = elapsedTime.ElapsedMilliseconds;

						if (state.readBytes == 4) {
							state.buffer = new byte[BitConverter.ToInt32(state.buffer, 0)];
						}

						if (state.readBytes - 4 == state.buffer.Length) {
							if (state.buffer[0] == (byte)OpCode.Texture) {
								controller.debugWriter.WriteLine("Filled texture buffer");
								controller.textureQueue.Add(new MultiplayerSkinBuffer(state.buffer, (int)state.buffer[1], (MPTextureType)state.buffer[2]));
							}else if (state.buffer[0] == (byte)OpCode.Connect) {
								bufferObjects.Add(new BufferObject(state.buffer, state.buffer.Length));
							}else if(state.buffer[0] == (byte)OpCode.Settings) {
								debugWriter.WriteLine("Adding player settings to queue");
								bufferObjects.Add(new BufferObject(state.buffer, state.buffer.Length));
							}else if(state.buffer[0] == (byte)OpCode.Disconnect) {
								bufferObjects.Add(new BufferObject(state.buffer, state.buffer.Length));
							}

							BeginReceivingTCP();
						} else {
							handler.BeginReceive(state.buffer, state.readBytes - 4, state.buffer.Length - state.readBytes + 4, SocketFlags.None, ReceiveCallbackTCP, state);
						}
					}
				} else {
					CloseConnection();
				}
			} catch (Exception e) {
				if (tcpConnection.Connected) {
					BeginReceivingTCP();
				} else {
					debugWriter.WriteLine(e.ToString());
					CloseConnection();
				}
			}
		}

		public void CloseConnection() {
			if (tcpConnection != null) {
				tcpConnection.Shutdown(SocketShutdown.Both);
				tcpConnection.Close();
			}
			if (udpConnection != null) {
				udpConnection.Close();
			}
			bufferObjects.Clear();
		}
	}
}
