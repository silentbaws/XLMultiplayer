using System.Diagnostics;
using UnityEngine;
using System.Text;
using XLMultiplayerUI;

namespace XLMultiplayer {
	class MultiplayerUtilityMenu : MonoBehaviour {
		private bool showStatus = true;

		// Encoding window variables
		public bool isLoading = false;
		public int loadingStatus = 0;
		Rect encodingWindowRect;

		Texture2D colorTexture;

		public int previousMessageCount = 0;
		public string chat = "";

		private Stopwatch importantChatWatch = new Stopwatch();

		private int importantChatDuration = 0;
		private bool wasOpenBeforeImportantChat;

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

			// Create a white 1x1 texture for drawing with
			colorTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
			colorTexture.SetPixel(0, 0, new Color(1, 1, 1, 1));
			colorTexture.Apply();
		}

		public void Update() {
			if (Input.GetKeyDown(KeyCode.Tab) && !importantChatWatch.IsRunning) {
				NewMultiplayerMenu.Instance.MessageInput.transform.parent.gameObject.SetActive(!NewMultiplayerMenu.Instance.MessageInput.transform.parent.gameObject.activeSelf);

				if (NewMultiplayerMenu.Instance.MessageInput.interactable) {
					NewMultiplayerMenu.Instance.MessageInput.interactable = false;
				}
			}


			if (Main.multiplayerController != null) {
				if (Main.multiplayerController.chatMessages != null && Main.multiplayerController.chatMessages.Count > 0) {
					int difference = Main.multiplayerController.chatMessages.Count - previousMessageCount;
					for (int i = Main.multiplayerController.chatMessages.Count - difference; i < Main.multiplayerController.chatMessages.Count; i++) {
						NewMultiplayerMenu.Instance.MessageBox.text += Main.multiplayerController.chatMessages[i] + "\n";
					}
					previousMessageCount = Main.multiplayerController.chatMessages.Count;
				}
			}

			if (importantChatWatch.IsRunning && importantChatWatch.ElapsedMilliseconds > importantChatDuration) {
				NewMultiplayerMenu.Instance.MessageInput.transform.parent.gameObject.SetActive(wasOpenBeforeImportantChat);
				importantChatWatch.Stop();
			}


			if (NewMultiplayerMenu.Instance.MessageInput.transform.parent.gameObject.activeSelf) {
				if (Input.GetKeyDown(KeyCode.T)) {
					NewMultiplayerMenu.Instance.MessageInput.interactable = true;
					NewMultiplayerMenu.Instance.MessageInput.Select();
					NewMultiplayerMenu.Instance.MessageInput.ActivateInputField();
				}
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

			if (messageStopwatch.IsRunning) {
				if (messageStopwatch.ElapsedMilliseconds > messageDuration) {
					messageStopwatch.Stop();
				}
			}

			if (isLoading) {
				GUI.backgroundColor = Color.black;
				GUI.contentColor = Color.yellow;

				encodingWindowRect = GUI.Window(1, encodingWindowRect, DisplayEncodingWindow, "Calm down it's loading");
			}
		}

		public void SendImportantChat(string message, int duration) {
			NewMultiplayerMenu.Instance.MessageBox.text += message + "\n";
			importantChatDuration = duration;

			wasOpenBeforeImportantChat = NewMultiplayerMenu.Instance.MessageInput.transform.parent.gameObject.activeSelf;
			NewMultiplayerMenu.Instance.MessageInput.transform.parent.gameObject.SetActive(true);

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
