using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

//TODO: Fix references to multiplayer manager

namespace XLMultiplayer {
	class MultiplayerStatusMenu : MonoBehaviour {
		private Texture2D connectedTexture;
		private Texture2D disconnectedTexture;

		private Canvas statusCanvas;

		private Image disconnectedImage;
		private Image connectedImage;

		private List<MultiplayerPlayerController> connectedPlayers;

		private bool showStatus = true;
		private string playerNames = "";
		private int numPlayers = 0;

		public bool isLoading = false;
		public int loadingStatus = 0;

		private string typedText = "";

		Rect windowRect;
		Rect chatWindowRect;

		Texture2D colorTexture;

		Vector2 chatScrollPosition = Vector2.zero;

		public int previousMessageCount = 0;
		public string chat = "";

		private bool refocused = false;

		private Stopwatch refocusWatch = new Stopwatch();

		float invalidVersionTimer = 0.0f;
		bool displayInvalidVersion = false;
		string serverVersion = "";

		float invalidMapTimer = 0.0f;
		bool displayInvalidMap = false;
		string serverMap = "";

		bool showVoteMenu = false;
		Vector2 scrollPosition = Vector2.zero;
		Rect mapVoteRect = new Rect(20, 10, 250, 420);

		bool showMapVote = false;
		Stopwatch voteWatch = new Stopwatch();

		public void Start() {
			InitializeMenu();

			windowRect = new Rect(Screen.width - Screen.width * 0.7f, Screen.height - Screen.height * 0.7f, Screen.width * 0.4f, Screen.height * 0.4f);
			chatWindowRect = new Rect(Screen.width - 400, Screen.height - 300, 400, 300);

			colorTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);

			colorTexture.SetPixel(0, 0, new Color(1, 1, 1, 1));

			colorTexture.Apply();
		}

		public void OnDestroy() {
			UnityEngine.Object.Destroy(statusCanvas);
			UnityEngine.Object.Destroy(connectedTexture);
			UnityEngine.Object.Destroy(disconnectedTexture);
		}

		public void InitializeMenu() {
			this.statusCanvas = new GameObject().AddComponent<Canvas>();
			this.statusCanvas.gameObject.AddComponent<GraphicRaycaster>();

			UnityEngine.Object.DontDestroyOnLoad(this.statusCanvas);

			CanvasScaler scaler = this.statusCanvas.gameObject.AddComponent<CanvasScaler>();
			scaler.scaleFactor = 10.0f;
			scaler.referenceResolution = new Vector2(1920, 1080);
			scaler.referencePixelsPerUnit = 10;
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

			this.statusCanvas.renderMode = RenderMode.ScreenSpaceCamera;


			this.disconnectedTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
			this.disconnectedTexture.LoadImage(File.ReadAllBytes(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\StatusRed64.png"));
			this.disconnectedTexture.filterMode = FilterMode.Point;
			this.disconnectedImage = new GameObject().AddComponent<Image>();
			this.disconnectedImage.sprite = Sprite.Create(disconnectedTexture, new Rect(0, 0, 64, 64), Vector2.zero);
			this.disconnectedImage.transform.SetParent(this.statusCanvas.transform, false);
			this.disconnectedImage.rectTransform.sizeDelta = new Vector2(64f, 64f);
			this.disconnectedImage.rectTransform.anchorMin = new Vector2(0.967f, 0.947f);
			this.disconnectedImage.rectTransform.anchorMax = new Vector2(0.987f, 0.967f);
			this.disconnectedImage.rectTransform.anchoredPosition = new Vector2(1f, 1f);
			AspectRatioFitter fitter = this.disconnectedImage.gameObject.AddComponent<AspectRatioFitter>();
			fitter.aspectRatio = 1.0f;
			fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
			this.disconnectedImage.color = Color.white;

			this.connectedTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
			this.connectedTexture.LoadImage(File.ReadAllBytes(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\StatusGreen64.png"));
			this.connectedTexture.filterMode = FilterMode.Point;
			this.connectedImage = new GameObject().AddComponent<Image>();
			this.connectedImage.sprite = Sprite.Create(connectedTexture, new Rect(0, 0, 64, 64), Vector2.zero);
			this.connectedImage.transform.SetParent(this.statusCanvas.transform, false);
			this.connectedImage.rectTransform.sizeDelta = new Vector2(64f, 64f);
			this.connectedImage.rectTransform.anchorMin = new Vector2(0.962f, 0.942f);
			this.connectedImage.rectTransform.anchorMax = new Vector2(0.987f, 0.967f);
			this.connectedImage.rectTransform.anchoredPosition = new Vector2(1f, 1f);
			AspectRatioFitter fitter2 = this.connectedImage.gameObject.AddComponent<AspectRatioFitter>();
			fitter2.aspectRatio = 1.0f;
			fitter2.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
			this.disconnectedImage.color = Color.white;
		}

		public void Update() {
			if (Input.GetKeyDown(KeyCode.Tab)) {
				showStatus = !showStatus;
			}

			if (Input.GetKeyDown(KeyCode.M)) {
				showVoteMenu = !showVoteMenu;
				if (showVoteMenu)
					XLShredLib.ModMenu.Instance.ShowCursor(Main.modId);
				else
					XLShredLib.ModMenu.Instance.HideCursor(Main.modId);
			}

			if (voteWatch.ElapsedMilliseconds > 5000) {
				showMapVote = false;
				voteWatch.Stop();
			}

			if (displayInvalidVersion)
				invalidVersionTimer += Time.unscaledDeltaTime;

			if (invalidVersionTimer > 10.0f) {
				displayInvalidVersion = false;
				invalidVersionTimer = 0.0f;
			}

			if (displayInvalidMap)
				invalidMapTimer += Time.unscaledDeltaTime;

			if (invalidMapTimer > 25.0f) {
				displayInvalidMap = false;
				invalidMapTimer = 0.0f;
			}

			if (this.connectedImage != null && this.disconnectedImage != null) {
				this.connectedImage.enabled = (Main.menu.multiplayerManager != null && Main.menu.multiplayerManager.runningClient) && showStatus;
				this.disconnectedImage.enabled = (Main.menu.multiplayerManager == null || !Main.menu.multiplayerManager.runningClient) && showStatus;
			}
			if (connectedPlayers == null && Main.menu.multiplayerManager != null)
				connectedPlayers = Main.menu.multiplayerManager.otherControllers;
			if (Main.menu.multiplayerManager != null && Main.menu.multiplayerManager.ourController != null)
				playerNames = Main.menu.multiplayerManager.ourController.username + "(YOU)\n";
			numPlayers = 0;
			if (connectedPlayers != null) {
				foreach (MultiplayerPlayerController player in connectedPlayers) {
					playerNames += player.username + "\n";
					numPlayers++;
				}
			}
		}

		public void StartVotePopup() {
			showMapVote = true;
			voteWatch = Stopwatch.StartNew();
		}

		public void DisplayInvalidVersion(string serverVersion) {
			displayInvalidVersion = true;
			invalidVersionTimer = 0.0f;
			this.serverVersion = serverVersion;
		}

		public void DisplayNoMap(string mapName) {
			displayInvalidMap = true;
			invalidMapTimer = 0.0f;
			this.serverMap = mapName;
		}

		public void DrawVoteMenu(int windowID) {
			GUI.DragWindow(new Rect(0, 0, 10000, 20));

			//Rect mapVoteRect = new Rect(20, 10, 250, 420);

			GUIStyle style = new GUIStyle(GUI.skin.verticalScrollbar);
			scrollPosition = GUI.BeginScrollView(new Rect(0, 20, 250, 320), scrollPosition, new Rect(0, 0, 250, 30 * MultiplayerUtils.serverMapDictionary.Count), false, true, GUIStyle.none, style);

			int count = 0;
			foreach (var item in MultiplayerUtils.serverMapDictionary) {
				GUI.backgroundColor = Color.grey;
				if (MultiplayerUtils.currentVote == item.Key)
					GUI.backgroundColor = Color.green;

				if (GUI.Button(new Rect(10, 30 * count, 220, 25), item.Value)) {
					byte[] mapVote = ASCIIEncoding.ASCII.GetBytes(item.Key);
					Main.menu.multiplayerManager.SendBytes(OpCode.MapVote, mapVote, true);
					MultiplayerUtils.currentVote = item.Key;
				}
				count++;
			}

			GUI.EndScrollView();
			GUI.Label(new Rect(10, 345, 230, 65), "Your vote is on the current map by default.\nDon't see a map you like on the list? Ask the host to add it.");
		}

		public void OnGUI() {
			if (showMapVote) {
				GUIStyle text = new GUIStyle();
				text.normal.textColor = Color.yellow;

				GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "Map voting has begun, more than half the server has requested a map change. You have 30 seconds to vote.", text);
			}

			if (showVoteMenu) {
				GUI.backgroundColor = Color.black;
				mapVoteRect = GUI.Window(3, mapVoteRect, DrawVoteMenu, "Map Vote");
			}

			if (displayInvalidVersion) {
				Rect rect = new Rect(Screen.width * 0.35f, 0, Screen.width * 0.55f, 400);

				GUIStyle style = new GUIStyle();
				style.fontSize = 24;
				style.wordWrap = true;
				style.alignment = TextAnchor.UpperCenter;

				GUI.Label(rect, "Tried to connect to server with different version than client\n Client version: " + Main.modEntry.Version.ToString() + "\nServer Version: " + serverVersion, style);
			}

			if (displayInvalidMap) {
				Rect rect = new Rect(Screen.width * 0.35f, 0, Screen.width * 0.55f, 400);

				GUIStyle style = new GUIStyle();
				style.fontSize = 24;
				style.wordWrap = true;
				style.alignment = TextAnchor.UpperCenter;

				GUI.Label(rect, "Tried to connect to server using a map you don't have. If you have the map either the client hasn't hashed all your maps, it's a different version from the servers, or it has a '.' in the name\nServer Map: " + serverMap, style);
			}

			if (showStatus && Main.menu.multiplayerManager != null && Main.menu.multiplayerManager.runningClient) {
				Rect rect = new Rect(Screen.width - Screen.width * 0.2f, Screen.width * 0.1f, Screen.width * 0.2f - 5, (numPlayers + 1) * 25f);

				GUI.contentColor = Color.black;
				GUIStyle nameStyle = new GUIStyle();
				nameStyle.fontSize = 16;
				nameStyle.alignment = TextAnchor.UpperRight;
				GUI.Label(rect, playerNames, nameStyle);

				NetworkClient client = Main.menu.multiplayerManager.client;

				Rect rect2 = new Rect(0, Screen.height - 105, Screen.width, 100);
				GUIStyle style = new GUIStyle();
				style.fontSize = 16;
				style.alignment = TextAnchor.LowerCenter;

				GUI.Label(rect2, "Ping: " + client.ping.ToString() + "\nPacket Loss: " + client.packetLoss.ToString() + "%", style);

				if (Main.menu.multiplayerManager.isConnected) {
					GUI.backgroundColor = Color.black;
					GUI.contentColor = Color.white;

					if (Input.GetKeyDown(KeyCode.T) && !GUI.GetNameOfFocusedControl().Equals("Text Chat")) {
						GUI.FocusControl("Text Chat");
						refocused = true;
						refocusWatch.Reset();
						refocusWatch.Start();
					}

					chatWindowRect = GUI.Window(2, chatWindowRect, DisplayChat, "Chat");
					if (chatWindowRect.x < 0) {
						chatWindowRect.x = 0;
					} else if (chatWindowRect.x + chatWindowRect.width > Screen.width) {
						chatWindowRect.x = Screen.width - chatWindowRect.width;
					}
					if (chatWindowRect.y < 0) {
						chatWindowRect.y = 0;
					} else if (chatWindowRect.y + chatWindowRect.height > Screen.height) {
						chatWindowRect.y = Screen.height - chatWindowRect.height;
					}
				}
			}


			if (isLoading) {
				GUI.backgroundColor = Color.black;
				GUI.contentColor = Color.yellow;

				windowRect = GUI.Window(1, windowRect, DisplayWindow, "Calm down it's loading");
			}
		}

		private void DisplayChat(int windowId) {
			GUI.DragWindow(new Rect(0, 0, 10000, 20));

			//Should probably just set this on launch but ¯\_(ツ)_ /¯
			GUIStyle style = new GUIStyle(GUI.skin.verticalScrollbar);
			style.fontSize = 14;
			style.alignment = TextAnchor.LowerLeft;
			style.normal.textColor = Color.white;
			style.wordWrap = true;

			GUIStyle style2 = new GUIStyle();
			style2.fontSize = 14;
			style2.alignment = TextAnchor.MiddleLeft;
			style2.normal.textColor = Color.white;
			style2.wordWrap = true;
			style2.richText = true;

			if (MultiplayerController.chatMessages.Count > 0) {
				int difference = MultiplayerController.chatMessages.Count - previousMessageCount;
				for (int i = MultiplayerController.chatMessages.Count - difference; i < MultiplayerController.chatMessages.Count; i++) {
					chat += MultiplayerController.chatMessages[i] + "\n";
				}
				previousMessageCount = MultiplayerController.chatMessages.Count;
				if (style2.CalcHeight(new GUIContent(chat), chatWindowRect.width - 26) > chatWindowRect.height - 43)
					chatScrollPosition.y += difference * style2.lineHeight;
			}

			chatScrollPosition = GUI.BeginScrollView(new Rect(3, 20, chatWindowRect.width - 6, chatWindowRect.height - 43), chatScrollPosition, new Rect(3, 0, chatWindowRect.width - 26, style2.CalcHeight(new GUIContent(chat), chatWindowRect.width - 26) - style2.lineHeight), false, true, GUIStyle.none, style);
			GUI.Label(new Rect(3, 0, chatWindowRect.width - 26, style2.CalcHeight(new GUIContent(chat), chatWindowRect.width - 26)), chat, style2);
			GUI.EndScrollView();

			GUI.DrawTexture(new Rect(3, chatWindowRect.height - 21, chatWindowRect.width - 6, 1), colorTexture);

			style2.wordWrap = false;


			if (Event.current.isKey && GUI.GetNameOfFocusedControl().Equals("Text Chat") && Event.current.keyCode == KeyCode.T) {
				if (refocusWatch.ElapsedMilliseconds < 250)
					Event.current.Use();
			}

			GUI.SetNextControlName("Text Chat");
			typedText = GUI.TextField(new Rect(3, chatWindowRect.height - 25, chatWindowRect.width - 6, 20), typedText, 1000, style2);

			if (refocused && GUI.GetNameOfFocusedControl().Equals("Text Chat")) {
				TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);

				if (te != null) {
					te.SelectNone();
					te.MoveTextEnd();
				}

				refocused = false;
			}

			Event current = Event.current;
			if (current.isKey && current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl().Equals("Text Chat")) {
				current.Use();
				Main.menu.multiplayerManager.SendChatMessage(typedText);
				typedText = "";
				GUI.FocusControl(null);
				GUI.UnfocusWindow();
			}
		}

		private void DisplayWindow(int windowId) {
			string loading = "Encoding shirt.......";

			//This is stupid, what was I thinking?
			if(loadingStatus > 0)
				loading += "\nEncoded Shirt\nEncoding Pants.......";
			if(loadingStatus > 1)
				loading += "\nEncoded Pants\nEncoding Shoes.......";
			if(loadingStatus > 2)
				loading += "\nEncoded Shoes\nEncoding Hat.......";
			if(loadingStatus > 3)
				loading += "\nEncoded Hat\nEncoding Board.......";
			if (loadingStatus > 4)
				loading += "\nEncoded Board";
			if (loadingStatus > 5)
				loading = "All Textures encoded, sending to server";

			GUIStyle style = new GUIStyle();
			style.fontSize = 16;
			style.alignment = TextAnchor.UpperCenter;
			style.normal.textColor = Color.yellow;
			GUI.Label(new Rect(0, 20, Screen.width * 0.4f, Screen.height * 0.4f - 20), loading, style);
		}
	}
}
