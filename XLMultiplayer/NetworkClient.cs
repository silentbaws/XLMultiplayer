using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

		public NetworkClient(string ipAddr, int port, MultiplayerController controller, StreamWriter sw) {
			this.controller = controller;
			this.debugWriter = sw;
			elapsedTime = new Stopwatch();
			elapsedTime.Start();
			try {
				ip = IPAddress.Parse(ipAddr.Trim());
			}catch(Exception e) {
				debugWriter.WriteLine(e.ToString());
				controller.KillConnection();
				return;
			}
			if (ip == null) {
				controller.KillConnection();
				return;
			}

			ipEndPoint = new IPEndPoint(ip, port);

			tcpConnection = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			udpConnection = new UdpClient();
			try {
				udpConnection.Connect(ipEndPoint);
			}catch(Exception e) {
				debugWriter.WriteLine("UDP Connection error {0}", e);
			}

			debugWriter.WriteLine("Begin connection tcp");
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
			} catch (Exception e) {
				debugWriter.WriteLine(e.ToString());
			}
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
			} catch (Exception e) {
				debugWriter.WriteLine("Error sending alive message {0}", e.ToString());
			}
		}

		long animDataSent = 0;
		int largestPacket = 0;
		int packetsSinceDebug = 0;

		public void SendUnreliable(byte[] buffer, OpCode opCode) {
			try {
				//Rearrange message before sending
				//Size of packet, opcode, packet sequence, rest of information
				byte[] packetSequence = BitConverter.GetBytes(opCode == OpCode.Position ? positionPackets : animationPackets);
				byte[] packetData = NetworkClient.Compress(buffer);
				byte[] packet = new byte[packetData.Length + packetSequence.Length + 1];

				packet[0] = (byte)opCode;

				Array.Copy(packetSequence, 0, packet, 1, 4);

				Array.Copy(packetData, 0, packet, 5, packetData.Length);

				largestPacket = Math.Max(largestPacket, packet.Length);

				udpConnection.Send(packet, packet.Length);

				if (opCode == OpCode.Position) {
					positionPackets++;
				} else {
					packetsSinceDebug++;
					animationPackets++;
					animDataSent += packet.Length;
					float average = animDataSent / animationPackets;
					if (packetsSinceDebug > 300) {
						debugWriter.WriteLine(opCode.ToString() + " average packet data length " + average + " largest packet " + largestPacket.ToString() + " current packet " + buffer.Length.ToString() + " uncompressed, " + packet.Length + " compressed");
						packetsSinceDebug = 0;
					}
				}
			} catch (Exception e) {
				debugWriter.WriteLine(e.ToString());
			}
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

				//Still alive is uncompressed udp
				if (buffer[0] != (byte)OpCode.StillAlive) {
					//Decompress the data before adding it to the queue
					byte[] compressedBuffer = new byte[buffer.Length - 6];
					Array.Copy(buffer, 5, compressedBuffer, 0, buffer.Length - 6);
					byte[] uncompressedBuffer = NetworkClient.Decompress(compressedBuffer);

					byte[] realBuffer = new byte[uncompressedBuffer.Length + 6];
					Array.Copy(buffer, 0, realBuffer, 0, 5);
					Array.Copy(uncompressedBuffer, 0, realBuffer, 5, uncompressedBuffer.Length);
					realBuffer[realBuffer.Length - 1] = buffer[buffer.Length - 1];

					bufferObjects.Add(new BufferObject(realBuffer, realBuffer.Length));
				} else {
					bufferObjects.Add(new BufferObject(buffer, buffer.Length));
				}

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
			try {
				tcpConnection = (Socket)ar.AsyncState;
				tcpConnection.EndConnect(ar);
				this.ipEndPoint = (IPEndPoint)tcpConnection.RemoteEndPoint;
				
				debugWriter.WriteLine(this.ipEndPoint.ToString());

				debugWriter.WriteLine(Main.modEntry.Version.ToString());

				byte[] versionString = ASCIIEncoding.ASCII.GetBytes(Main.modEntry.Version.ToString());
				byte[] versionMessage = new byte[versionString.Length + 5];

				Array.Copy(BitConverter.GetBytes(versionString.Length + 1), 0, versionMessage, 0, 4);
				versionMessage[4] = (byte)OpCode.VersionNumber;
				Array.Copy(versionString, 0, versionMessage, 5, versionString.Length);

				tcpConnection.Send(versionMessage);

				BeginReceivingTCP();
				BeginReceivingUDP();
			} catch(Exception e) {
				debugWriter.WriteLine(e.ToString());
			}
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
						lastAlive = elapsedTime.ElapsedMilliseconds > lastAlive ? elapsedTime.ElapsedMilliseconds : lastAlive;

						if (state.readBytes == 4) {
							state.buffer = new byte[BitConverter.ToInt32(state.buffer, 0)];
						}
						if (state.readBytes - 4 == state.buffer.Length) {
							if (state.buffer[0] == (byte)OpCode.Texture) {
								controller.debugWriter.WriteLine("Filled texture buffer");
								controller.textureQueue.Add(new MultiplayerSkinBuffer(state.buffer, (int)state.buffer[1], (MPTextureType)state.buffer[2]));
							} else if (state.buffer[0] == (byte)OpCode.Connect) {
								bufferObjects.Add(new BufferObject(state.buffer, state.buffer.Length));
							} else if (state.buffer[0] == (byte)OpCode.Chat) {
								bufferObjects.Add(new BufferObject(state.buffer, state.buffer.Length));
							} else if (state.buffer[0] == (byte)OpCode.Settings) {
								debugWriter.WriteLine("Adding player settings to queue");
								bufferObjects.Add(new BufferObject(state.buffer, state.buffer.Length));
							} else if (state.buffer[0] == (byte)OpCode.MapList) {
							} else if (state.buffer[0] == (byte)OpCode.Disconnect) {
								Main.menu.multiplayerManager.ProcessMessage(state.buffer, state.buffer.Length);
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
			try {
				if (tcpConnection != null) {
					tcpConnection.Shutdown(SocketShutdown.Both);
					tcpConnection.Close();
				}
				if (udpConnection != null) {
					udpConnection.Close();
				}
				bufferObjects.Clear();
			} catch(Exception e) {
				debugWriter.WriteLine(e.ToString());
			}
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
	}
}
