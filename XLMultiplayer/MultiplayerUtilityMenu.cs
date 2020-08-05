using System.Diagnostics;
using UnityEngine;
using System.Text;

namespace XLMultiplayer {
	class MultiplayerUtilityMenu : MonoBehaviour {
		private bool showStatus = true;

		// Encoding window variables
		public bool isLoading = false;
		public int loadingStatus = 0;
		Rect encodingWindowRect;

		// Chat window variables
		Rect chatWindowRect;
		Vector2 chatScrollPosition = Vector2.zero;

		Texture2D colorTexture;

		public int previousMessageCount = 0;
		public string chat = "";
		private string typedText = "";

		private bool refocused = false;
		private Stopwatch refocusWatch = new Stopwatch();
		private Stopwatch importantChatWatch = new Stopwatch();

		private int importantChatDuration = 0;

		GUIStyle chatStyle1 = null;
		GUIStyle chatStyle2 = null;

		// Map voting window variables
		bool showVoteMenu = false;
		Rect mapVoteRect = new Rect(20, 10, 250, 420);
		Vector2 scrollPosition = Vector2.zero;

		// messageWindowRect variables
		Rect messageWindowRect;
		private Stopwatch messageStopwatch = new Stopwatch();
		private int messageDuration = 0;
		private string messageWindowTitle = "";
		private string messageWindowContent = "";

		public void Start() {
			// Create windows
			encodingWindowRect = new Rect(Screen.width - Screen.width * 0.7f, Screen.height - Screen.height * 0.7f, Screen.width * 0.4f, Screen.height * 0.4f);
			messageWindowRect = new Rect(Screen.width - Screen.width * 0.7f, Screen.height - Screen.height * 0.7f, Screen.width * 0.4f, Screen.height * 0.4f);
			chatWindowRect = new Rect(Screen.width - 400, Screen.height - 300, 400, 300);

			// Create a white 1x1 texture for drawing with
			colorTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
			colorTexture.SetPixel(0, 0, new Color(1, 1, 1, 1));
			colorTexture.Apply();
		}

		public void Update() {
			if (Input.GetKeyDown(KeyCode.Tab)) {
				showStatus = !showStatus;
			}

			if (Input.GetKeyDown(KeyCode.M)) {
				showVoteMenu = !showVoteMenu;
				Cursor.visible = showVoteMenu;
			}
		}

		public void OnGUI() {
			if (showVoteMenu) {
				GUI.backgroundColor = Color.black;
				mapVoteRect = GUI.Window(3, mapVoteRect, DrawVoteMenu, "Map Vote");
			}

			if ((showStatus && Main.multiplayerController != null && Main.multiplayerController.isConnected) || importantChatWatch.IsRunning) {
				if (importantChatWatch.IsRunning && importantChatWatch.ElapsedMilliseconds > importantChatDuration) {
					importantChatWatch.Stop();
				}

				GUI.backgroundColor = Color.black;
				GUI.contentColor = Color.white;

				if (Input.GetKeyDown(KeyCode.T) && !GUI.GetNameOfFocusedControl().Equals("Text Chat")) {
					GUI.FocusControl("Text Chat");
					refocused = true;
					refocusWatch.Restart();
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

			if (messageStopwatch.IsRunning) {
				if(messageStopwatch.ElapsedMilliseconds > messageDuration) {
					messageStopwatch.Stop();
				}
				GUI.backgroundColor = Color.black;
				GUI.contentColor = Color.white;

				messageWindowRect = GUI.Window(4, messageWindowRect, DisplayMessageWindow, messageWindowTitle);
			}

			if (isLoading) {
				GUI.backgroundColor = Color.black;
				GUI.contentColor = Color.yellow;

				encodingWindowRect = GUI.Window(1, encodingWindowRect, DisplayEncodingWindow, "Calm down it's loading");
			}
		}

		public void SendImportantChat(string message, int duration) {
			chat += message + "\n";
			importantChatDuration = duration;
			if (chatStyle2 != null) {
				chatStyle2.wordWrap = true;
				chatScrollPosition.y = chatStyle2.CalcHeight(new GUIContent(chat), chatWindowRect.width - 26);
			}
			importantChatWatch.Restart();
		}

		public void DisplayMessage(string title, string content, int duration) {
			messageWindowContent = content;
			messageWindowTitle = title;
			messageDuration = duration;
			messageStopwatch.Restart();
		}

		private void DisplayMessageWindow(int windowId) {
			GUIStyle style = new GUIStyle();
			style.fontSize = 16;
			style.alignment = TextAnchor.UpperCenter;
			style.normal.textColor = Color.yellow;
			style.wordWrap = true;

			GUI.Label(new Rect(0, 20, Screen.width * 0.4f, Screen.height * 0.4f - 20), messageWindowContent, style);
		}

		private void DisplayChat(int windowId) {
			GUI.DragWindow(new Rect(0, 0, 10000, 20));

			if (chatStyle1 == null || chatStyle2 == null) {
				UnityModManagerNet.UnityModManager.Logger.Log("Setting styles");

				// Create styles for chat
				chatStyle1 = new GUIStyle(GUI.skin.verticalScrollbar);
				chatStyle1.fontSize = 14;
				chatStyle1.alignment = TextAnchor.LowerLeft;
				chatStyle1.normal.textColor = Color.white;
				chatStyle1.wordWrap = true;

				chatStyle2 = new GUIStyle();
				chatStyle2.fontSize = 14;
				chatStyle2.alignment = TextAnchor.MiddleLeft;
				chatStyle2.normal.textColor = Color.white;
				chatStyle2.wordWrap = true;
				chatStyle2.richText = true;
			}

			chatStyle2.wordWrap = true;
			if (Main.multiplayerController != null) {
				if (Main.multiplayerController.chatMessages != null && Main.multiplayerController.chatMessages.Count > 0) {
					int difference = Main.multiplayerController.chatMessages.Count - previousMessageCount;
					float scrollDifference = 0f;
					for (int i = Main.multiplayerController.chatMessages.Count - difference; i < Main.multiplayerController.chatMessages.Count; i++) {
						chat += Main.multiplayerController.chatMessages[i] + "\n";
						scrollDifference += chatStyle2.CalcHeight(new GUIContent(Main.multiplayerController.chatMessages[i]), chatWindowRect.width - 26) / chatStyle2.CalcHeight(new GUIContent("a"), chatWindowRect.width - 26) * chatStyle2.lineHeight;
					}
					previousMessageCount = Main.multiplayerController.chatMessages.Count;
					if (chatStyle2.CalcHeight(new GUIContent(chat), chatWindowRect.width - 26) > chatWindowRect.height - 43)
						chatScrollPosition.y += scrollDifference;
				}
			}

			// Create the chat window
			chatScrollPosition = GUI.BeginScrollView(new Rect(3, 20, chatWindowRect.width - 6, chatWindowRect.height - 43), chatScrollPosition, new Rect(3, 0, chatWindowRect.width - 26, chatStyle2.CalcHeight(new GUIContent(chat), chatWindowRect.width - 26) - chatStyle2.lineHeight), false, true, GUIStyle.none, chatStyle1);
			GUI.Label(new Rect(3, 0, chatWindowRect.width - 26, chatStyle2.CalcHeight(new GUIContent(chat), chatWindowRect.width - 26)), chat, chatStyle2);
			GUI.EndScrollView();

			GUI.DrawTexture(new Rect(3, chatWindowRect.height - 21, chatWindowRect.width - 6, 1), colorTexture);

			// Prevent accidently typing t's in chat immediately after focusing it
			if (Event.current.isKey && GUI.GetNameOfFocusedControl().Equals("Text Chat") && Event.current.keyCode == KeyCode.T) {
				if (refocusWatch.ElapsedMilliseconds < 250)
					Event.current.Use();
			}

			chatStyle2.wordWrap = false;
			// Create the text field for typing and name it "Text Chat"
			GUI.SetNextControlName("Text Chat");
			typedText = GUI.TextField(new Rect(3, chatWindowRect.height - 25, chatWindowRect.width - 6, 20), typedText, 1000, chatStyle2);

			// Move typing position to the end of the message when you focus the chat window
			if (refocused && GUI.GetNameOfFocusedControl().Equals("Text Chat")) {
				TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);

				if (te != null) {
					te.SelectNone();
					te.MoveTextEnd();
				}

				refocused = false;
			}

			// Send chat message and unfocus after sending message
			Event current = Event.current;
			if (current.isKey && current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl().Equals("Text Chat")) {
				current.Use();
				Main.multiplayerController.SendChatMessage(typedText);
				typedText = "";
				GUI.FocusControl(null);
				GUI.UnfocusWindow();
			}
		}

		private void DisplayEncodingWindow(int windowId) {
			string loading = "Encoding shirt.......";

			if (loadingStatus > 0) loading += "\nEncoded Shirt\nEncoding Pants.......";
			if (loadingStatus > 1) loading += "\nEncoded Pants\nEncoding Shoes.......";
			if (loadingStatus > 2) loading += "\nEncoded Shoes\nEncoding Hat.......";
			if (loadingStatus > 3) loading += "\nEncoded Hat\nEncoding Deck.......";
			if (loadingStatus > 4) loading += "\nEncoded Deck\nEncoding Griptape.......";
			if (loadingStatus > 5) loading += "\nEncoded Griptape\nEncoding Trucks.......";
			if (loadingStatus > 6) loading += "\nEncoded Trucks\nEncoding Wheels.......";
			if (loadingStatus > 7) loading = "All Textures encoded, sending to server";

			GUIStyle style = new GUIStyle();
			style.fontSize = 16;
			style.alignment = TextAnchor.UpperCenter;
			style.normal.textColor = Color.yellow;
			GUI.Label(new Rect(0, 20, Screen.width * 0.4f, Screen.height * 0.4f - 20), loading, style);
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
					Main.multiplayerController.SendBytes(OpCode.MapVote, mapVote, true);
					MultiplayerUtils.currentVote = item.Key;
				}
				count++;
			}

			GUI.EndScrollView();
			GUI.Label(new Rect(10, 345, 230, 65), "Your vote is in green!\nDon't see a map you like on the list? Ask the host to add it.");
		}
	}
}
