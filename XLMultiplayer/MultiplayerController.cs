using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Valve.Sockets;

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
		private const int maxMessages = 100;
		private NetworkingMessage[] netMessages;

		private StreamWriter debugWriter;

		private MultiplayerLocalPlayerController playerController;
		private List<MultiplayerRemotePlayerController> remoteControllers = new List<MultiplayerRemotePlayerController>();

		// Open replay editor on start to prevent null references to replay editor instance
		public void Start() {
			if (ReplayEditor.ReplayEditorController.Instance == null) {
				GameManagement.GameStateMachine.Instance.ReplayObject.SetActive(true);
				StartCoroutine(TurnOffReplay());
			}
		}

		// Turn off replay editor as soon as it's instance is not null
		private IEnumerator TurnOffReplay() {
			while (ReplayEditor.ReplayEditorController.Instance == null)
				yield return new WaitForEndOfFrame();

			GameManagement.GameStateMachine.Instance.ReplayObject.SetActive(false);
			yield break;
		}

		public void ConnectToServer(string ip, ushort port) {
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
						// Client connected to server

						// Start new send update thread

						// Encode Textures coroutine

						// Send textures on connection
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

		public void SendTextures() {
			// Send this
			playerController.shirtMPTex.GetSendData();
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
