using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Valve.Sockets;

namespace XLMultiplayer {
	class MultiplayerController : MonoBehaviour{
		NetworkingSockets client = null;
		StatusCallback status = null;
		uint connection;
		const int maxMessages = 100;
		NetworkingMessage[] netMessages;

		StreamWriter debugWriter;

		public void ConnectToServer(string ip, ushort port) {
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

			Library.Initialize();

			client = new NetworkingSockets();

			Address addy = new Address();
			addy.SetAddress(ip, port);

			connection = client.Connect(ref addy);

			status = (info, context) => {
				switch (info.connectionInfo.state) {
					case ConnectionState.None:
						break;

					case ConnectionState.Connected:
						//Client connected to server
						
						// Send textures on connection

						// Start new send update thread
						break;

					case ConnectionState.ClosedByPeer:
						//Client disconnected from server
						DisconnectFromServer();
						break;

					case ConnectionState.ProblemDetectedLocally:
						//Client unable to connect
						DisconnectFromServer();
						break;
				}
			};

			netMessages = new NetworkingMessage[maxMessages];
		}

		public void Update() {
			if (client == null) return;

			if (GameManagement.GameStateMachine.Instance.CurrentState.GetType() != typeof(GameManagement.ReplayState)) {
				// Turn on username renderer and turn off replay controllersS
			} else {
				// Setup replay state on transition to replay state
			}

			UpdateClient();

			// Lerp frames
		}

		private void UpdateClient() {
			client.DispatchCallback(status);

			// Read messages

			// Save textures from queue

			// Apply saved textures
		}

		private string RemoveMarkup(string msg) {
			string old;

			do {
				old = msg;
				msg = Regex.Replace(msg.Trim(), "</?(?:b|i|color|size|material|quad)[^>]*>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			} while (!msg.Equals(old));

			return msg;
		}

		private void CleanupSockets() {
			Library.Deinitialize();

			client = null;
			status = null;
			netMessages = null;
		}

		public void DisconnectFromServer() {
			client.CloseConnection(connection);

			CleanupSockets();
		}

		public void OnDestroy() {
			DisconnectFromServer();
		}
	}
}
