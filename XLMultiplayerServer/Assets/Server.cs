using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Types;


public enum MPTextureType : byte {
	Shirt = 0,
	Pants = 1,
	Shoes = 2,
	Hat = 3,
	Board = 4
}

public class Player {
	public int connectionID;
	public string username;

	public MultiplayerSkin Pants, Shirt, Hat, Shoes, Board;

	public bool packedAll = false;
	public bool sentAll = false;

	public string IP;

	public Player(int cID, string user) {
		connectionID = cID;
		username = user;
		
		int port;
		NetworkID network;
		NodeID dstNode;
		NetworkTransport.GetConnectionInfo(Server.hostId, this.connectionID, out this.IP, out port, out network, out dstNode, out Server.error);

		Pants = new MultiplayerSkin(MPTextureType.Pants);
		Shirt = new MultiplayerSkin(MPTextureType.Shirt);
		Hat = new MultiplayerSkin(MPTextureType.Hat);
		Shoes = new MultiplayerSkin(MPTextureType.Shoes);
		Board = new MultiplayerSkin(MPTextureType.Board);
	}
}

public class MultiplayerSkin {
	Vector2 size;
	MPTextureType textureType;

	public bool finishedCopy = false;

	int bufferSize = 0;

	public MultiplayerSkin(MPTextureType mpType) {
		textureType = mpType;
	}

	public string GetTexturePath(int connectionId) {
		return Directory.GetCurrentDirectory() + "\\TempClothing\\" + textureType.ToString() + connectionId.ToString() + ".png";
	}

	public byte[] GetPreBuffer(int connectionId) {
		byte[] buffer = new byte[14];
		Array.Copy(BitConverter.GetBytes(bufferSize + 10), 0, buffer, 0, 4);
		buffer[4] = (byte)connectionId;
		buffer[5] = (byte)textureType;
		Array.Copy(BitConverter.GetBytes(size.x), 0, buffer, 6, 4);
		Array.Copy(BitConverter.GetBytes(size.y), 0, buffer, 10, 4);

		return buffer;
	}

	public void SaveTexture(int connectionId, byte[] buffer) {
		size = new Vector2(BitConverter.ToSingle(buffer, 1), BitConverter.ToSingle(buffer, 5));
		byte[] file = new byte[buffer.Length - 9];
		Array.Copy(buffer, 9, file, 0, file.Length);

		bufferSize = file.Length;

		if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\TempClothing"))
			Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\TempClothing");

		File.WriteAllBytes(Directory.GetCurrentDirectory() + "\\TempClothing\\" + textureType.ToString() + connectionId.ToString() + ".png", file);
		finishedCopy = true;
	}

	public void DeleteTexture(int connectionId) {
		File.Delete(Directory.GetCurrentDirectory() + "\\TempClothing\\" + textureType.ToString() + connectionId.ToString() + ".png");
	}
}

public class Server : MonoBehaviour
{
	private byte reliableChannel;
	private byte unreliableChannel;
	private byte reliableSequenceChannel;

	public static int hostId;

	public static byte error;

	public static List<Player> connections = new List<Player>();

	private FileServer fileServer;

	private readonly HttpClient client = new HttpClient();
	private readonly string version = "0.0.1";
	private readonly string main_server = "https://sxl-server-announcer.herokuapp.com/v1";

	private string server_name = "my server"; // need to load from settings file
	private string map = "my map"; // good luck with this one

	public static void WriteLine(string value){
		Console.WriteLine(value);
		Debug.Log(value);
	}

	public void Start()
	{
		Application.runInBackground = true;
		WriteLine("Attempting to start server");

		NetworkTransport.Init();
		
		ConnectionConfig connectionConfig = new ConnectionConfig();
		connectionConfig.PacketSize = 1400;
		connectionConfig.MaxSentMessageQueueSize = 1024;
		connectionConfig.InitialBandwidth = 15360000;
		connectionConfig.SendDelay = 10;
		connectionConfig.ResendTimeout = 600;
		connectionConfig.AcksType = ConnectionAcksType.Acks128;
		this.reliableChannel = connectionConfig.AddChannel(QosType.Reliable);
		this.unreliableChannel = connectionConfig.AddChannel(QosType.UnreliableSequenced);
		this.reliableSequenceChannel = connectionConfig.AddChannel(QosType.ReliableSequenced);

		HostTopology topology = new HostTopology(connectionConfig, 10);
		hostId = NetworkTransport.AddHost(topology, 7777, null);
		if (hostId < 0)
		{
			WriteLine("Server socket creation failed");
		}
		else
		{
			WriteLine("Server socket creation successful");
		}

		fileServer = new FileServer(7778);

		StartCoroutine(SendAnnounce());
	}

	private IEnumerator SendAnnounce() {
		while (true) {
			yield return new WaitForSeconds(5);
			WWWForm form = new WWWForm();
			form.AddField("name", server_name);
			form.AddField("n_players", "" + connections.Count);
			form.AddField("map", map);
			form.AddField("version", version);
			using (UnityWebRequest www = UnityWebRequest.Post(main_server, form)) {
				yield return www.SendWebRequest();
				if (www.isNetworkError || www.isHttpError) {
					WriteLine("Failed announcement to main server: " + www.error + ": " + www.downloadHandler.text);
				}
			}
		}
	}

	private void Update()
	{
		byte[] buffer = new byte[1024];
		int hId;
		int conId;
		int chanId;
		int bufSize;
		NetworkEventType networkEvent = NetworkTransport.Receive(out hId, out conId, out chanId, buffer, 1024, out bufSize, out error);
		while (networkEvent != NetworkEventType.Nothing)
		{
			if (error != (int)NetworkError.Ok)
				WriteLine("Error recieving event" + ((NetworkError)error).ToString());
			switch (networkEvent)
			{
				case NetworkEventType.ConnectEvent:
					WriteLine("Recieved Connection Event From " + conId.ToString());
					connections.Add(new Player(conId, ""));

					SendToAll(new byte[] { 254 }, 1, conId, this.reliableChannel);

					byte[] newConnection = new byte[5];
					newConnection[0] = 254;
					for(int i = 0; i < connections.Count; i ++)
					{
						if (connections[i].connectionID != conId) {
							Array.Copy(BitConverter.GetBytes(connections[i].connectionID), 0, newConnection, 1, 4);
							NetworkTransport.Send(hostId, conId, reliableChannel, newConnection, 5, out error);
							if (error != (int)NetworkError.Ok)
								WriteLine("Error sending existing players to " + conId.ToString() + " " + error.ToString());
						}
					}
					break;
				case NetworkEventType.DataEvent:
					if (buffer[0] == 0 || buffer[0] == 1 || buffer[0] == 2)
					{
						SendToAll(buffer, bufSize, conId, chanId);
						if(buffer[0] == 2) {
							int index = -1;
							for(int i = 0; i < connections.Count; i++) {
								if(connections[i].connectionID == conId) {
									index = i;
									break;
								}
							}
							if (index != -1) {
								connections[index].username = Encoding.ASCII.GetString(buffer, 1, bufSize - 1);
								WriteLine("Recieved username " + connections[index].username);
								foreach (Player player in connections) {
									if(player.connectionID != conId) {
										Byte[] newBuffer = new byte[5 + player.username.Length];
										newBuffer[0] = 2;
										Array.Copy(Encoding.ASCII.GetBytes(player.username), 0, newBuffer, 1, player.username.Length);
										Array.Copy(BitConverter.GetBytes(player.connectionID), 0, newBuffer, newBuffer.Length - 4, 4);
										NetworkTransport.Send(hostId, connections[index].connectionID, reliableChannel, newBuffer, newBuffer.Length, out error);
									}
								}
							}
						}
					}
					break;
				case NetworkEventType.DisconnectEvent:
					WriteLine("Recieved Disconnect Event From " + conId.ToString());
					for(int i = 0; i < connections.Count; i++) {
						if (connections[i].connectionID == conId) {
							connections[i].Pants.DeleteTexture(connections[i].connectionID);
							connections[i].Shirt.DeleteTexture(connections[i].connectionID);
							connections[i].Shoes.DeleteTexture(connections[i].connectionID);
							connections[i].Board.DeleteTexture(connections[i].connectionID);
							connections[i].Hat.DeleteTexture(connections[i].connectionID);
							connections.RemoveAt(i);
						}
					}
					SendToAll(new byte[] { 255 }, 1, conId, this.reliableChannel);
					GC.Collect();
					GC.WaitForPendingFinalizers();
					break;
			}
			networkEvent = NetworkTransport.Receive(out hId, out conId, out chanId, buffer, 1024, out bufSize, out error);
		}

		foreach(FileServer.Client client in FileServer.clients) {
			if (client.newConnection && client.socket.Connected) {
				foreach (Player player in connections) {
					if(player.connectionID != client.connectionId && player.packedAll) {
						WriteLine("Sending old textures to new player " + client.connectionId.ToString() + " from " + player.connectionID.ToString());
						client.socket.SendFile(player.Pants.GetTexturePath(player.connectionID), player.Pants.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
						client.socket.SendFile(player.Shirt.GetTexturePath(player.connectionID), player.Shirt.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
						client.socket.SendFile(player.Shoes.GetTexturePath(player.connectionID), player.Shoes.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
						client.socket.SendFile(player.Board.GetTexturePath(player.connectionID), player.Board.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
						client.socket.SendFile(player.Hat.GetTexturePath(player.connectionID), player.Hat.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
					}
					client.newConnection = false;
				}
			}
		}

		foreach (Player player in connections) {
			if(!player.packedAll)
				if (player.Hat.finishedCopy && player.Board.finishedCopy && player.Shoes.finishedCopy && player.Shirt.finishedCopy && player.Pants.finishedCopy)
					player.packedAll = true;
		//	if (player.packedAll && !player.sentAll) {
		//		if(FileServer.clients.Count > 1) {
		//			foreach(FileServer.Client client in FileServer.clients) {
		//				if(client.connectionId != player.connectionID) {
		//					WriteLine("Sending Textures");
		//					client.socket.SendFile(player.Pants.GetTexturePath(player.connectionID), player.Pants.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
		//					client.socket.SendFile(player.Shirt.GetTexturePath(player.connectionID), player.Shirt.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
		//					client.socket.SendFile(player.Shoes.GetTexturePath(player.connectionID), player.Shoes.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
		//					client.socket.SendFile(player.Board.GetTexturePath(player.connectionID), player.Board.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
		//					client.socket.SendFile(player.Hat.GetTexturePath(player.connectionID), player.Hat.GetPreBuffer(player.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
		//				}
		//			}
		//		}
		//		player.sentAll = true;
		//	}
		}
	}

	private void SendToAll(byte[] buffer, int bufSize, int fromId, int channel)
	{
		byte[] newBuffer = new byte[bufSize + 4];
		Array.Copy(buffer, newBuffer, bufSize);
		Array.Copy(BitConverter.GetBytes(fromId), 0, newBuffer, bufSize, 4);

		for (int i = 0; i < connections.Count; i++) {
			if (connections[i].connectionID != fromId)
			{
				NetworkTransport.Send(hostId, connections[i].connectionID, channel, newBuffer, bufSize + 4, out error);
				if(error != (int)NetworkError.Ok)
					WriteLine("Error sending message to " + connections[i].connectionID.ToString() + " " + error.ToString());
			}
		}
	}

	public class FileServer {
		public Socket listener;
		public int port;

		public static List<Client> clients = new List<Client>();

		public class StateObject {
			public Socket workSocket = null;
			public byte[] buffer;
			public int readBytes = 0;
		}

		public class Client {
			public Socket socket { get; set; }
			public ReceivePacket Receive { get; set; }
			public int connectionId;
			public bool newConnection = true;

			public Client(Socket socket, int connectionId) {
				Receive = new ReceivePacket(socket, this, connectionId);
				this.socket = socket;
				this.connectionId = connectionId;
				Receive.StartReceiving();

				//foreach(Player p in Server.connections) {
				//	if (connectionId != p.connectionID && p.packedAll) {
				//		Server.WriteLine("Sending new player texture from " + p.connectionID.ToString());
				//		socket.SendFile(p.Pants.GetTexturePath(p.connectionID), p.Pants.GetPreBuffer(p.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
				//		socket.SendFile(p.Shirt.GetTexturePath(p.connectionID), p.Shirt.GetPreBuffer(p.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
				//		socket.SendFile(p.Shoes.GetTexturePath(p.connectionID), p.Shoes.GetPreBuffer(p.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
				//		socket.SendFile(p.Board.GetTexturePath(p.connectionID), p.Board.GetPreBuffer(p.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
				//		socket.SendFile(p.Hat.GetTexturePath(p.connectionID), p.Hat.GetPreBuffer(p.connectionID), null, TransmitFileOptions.UseDefaultWorkerThread);
				//	}
				//}
			}
		}

		public class ReceivePacket {
			private Socket receiveSocket;
			private Client client;
			private Player player;

			public ReceivePacket(Socket receiveSocket, Client client, int connectionId) {
				this.receiveSocket = receiveSocket;
				this.client = client;
				foreach(Player p in Server.connections) {
					if(p.connectionID == connectionId) {
						player = p;
						break;
					}
				}
			}

			public void StartReceiving() {
				StateObject state = new StateObject();
				state.workSocket = receiveSocket;
				state.buffer = new byte[4];
				state.readBytes = 0;
				receiveSocket.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None, ReceiveCallback, state);
			}

			public void ReceiveCallback(IAsyncResult ar) {
				try {
					StateObject state = (StateObject)ar.AsyncState;
					Socket handler = state.workSocket;
					int bytesRead = handler.EndReceive(ar);

					if (bytesRead > 0) {
						state.readBytes += bytesRead;
						Server.WriteLine("read " + state.readBytes.ToString() + " bytes of " + state.buffer.Length.ToString());
						if (state.readBytes < 4) {
							handler.BeginReceive(state.buffer, state.readBytes, state.buffer.Length - state.readBytes, SocketFlags.None, ReceiveCallback, state);
						} else {
							if (state.readBytes == 4) {
								state.buffer = new byte[BitConverter.ToInt32(state.buffer, 0)];
							}

							if (state.readBytes - 4 == state.buffer.Length) {
								switch ((MPTextureType)state.buffer[0]) {
									case MPTextureType.Pants:
										player.Pants.SaveTexture(player.connectionID, state.buffer);
										break;
									case MPTextureType.Shirt:
										player.Shirt.SaveTexture(player.connectionID, state.buffer);
										break;
									case MPTextureType.Shoes:
										player.Shoes.SaveTexture(player.connectionID, state.buffer);
										break;
									case MPTextureType.Board:
										player.Board.SaveTexture(player.connectionID, state.buffer);
										break;
									case MPTextureType.Hat:
										player.Hat.SaveTexture(player.connectionID, state.buffer);
										break;
								}

								StartReceiving();
							} else { 
								handler.BeginReceive(state.buffer, state.readBytes - 4, state.buffer.Length - state.readBytes + 4, SocketFlags.None, ReceiveCallback, state);
							}
						}
					} else {
						Disconnect();
					}
				}catch(Exception e) {
					if (receiveSocket.Connected) {
						StartReceiving();
					} else {
						Disconnect();
					}
				}
			}

			private void Disconnect() {
				if (receiveSocket.Connected)
					receiveSocket.Disconnect(true);
				if (clients.Contains(client))
					clients.Remove(client);
				WriteLine("Disconnect from " + client.connectionId.ToString() + " on file transfer server");
			}
		}

		public FileServer(int port) {
			listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			this.port = port;
			StartListening();
		}

		public void StartListening() {
			try {
				Server.WriteLine("Started listening on file transfer server");
				listener.Bind(new IPEndPoint(IPAddress.Any, port));
				listener.Listen(10);
				listener.BeginAccept(AcceptCallback, listener);
			} catch(Exception e) {

			}
		}

		public void AcceptCallback(IAsyncResult ar) {
			try {
				Server.WriteLine("Began connection on file transfer server");
				Socket acceptedSocket = listener.EndAccept(ar);
				int connectionId = -1;
				foreach (Player player in Server.connections) {
					if (IPAddress.Parse(player.IP).MapToIPv6().ToString() == ((IPEndPoint)acceptedSocket.RemoteEndPoint).Address.MapToIPv6().ToString()) {
						connectionId = player.connectionID;
						Server.WriteLine("Connection " + player.connectionID.ToString() + " has joined the file transfer server");
					}
				}

				if (connectionId != -1) {
					clients.Add(new Client(acceptedSocket, connectionId));
				} else {
					Server.WriteLine("Disconnected " + ((IPEndPoint)acceptedSocket.RemoteEndPoint).Address.ToString() + " from file transfer server");
					acceptedSocket.Disconnect(true);
				}
				listener.BeginAccept(AcceptCallback, listener);
			} catch(Exception e) {
				Server.WriteLine(e.ToString());
			}
		}
	}
}
