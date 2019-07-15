using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace XLMultiplayer {
	class MultiplayerStatusMenu : MonoBehaviour{
		private Texture2D connectedTexture;
		private Texture2D disconnectedTexture;

		private Canvas statusCanvas;

		private Image disconnectedImage;
		private Image connectedImage;

		private List<MultiplayerPlayerController> connectedPlayers;

		private bool showStatus = true;
		private string playerNames;
		private int numPlayers = 0;

		public void Start() {
			InitializeMenu();
		}

		public void InitializeMenu() {
			string texturePath = "file:\\\\\\" + Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\";

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

			if (this.connectedImage != null && this.disconnectedImage != null) {
				this.connectedImage.enabled = (Main.menu.multiplayerManager != null && Main.menu.multiplayerManager.runningClient) && showStatus;
				this.disconnectedImage.enabled = (Main.menu.multiplayerManager == null || !Main.menu.multiplayerManager.runningClient) && showStatus;
			}
			if (connectedPlayers == null && Main.menu.multiplayerManager != null)
				connectedPlayers = Main.menu.multiplayerManager.otherControllers;
			if(Main.menu.multiplayerManager != null && Main.menu.multiplayerManager.ourController != null)
				playerNames = Main.menu.multiplayerManager.ourController.username + "(YOU)\n";
			numPlayers = 0;
			if (connectedPlayers != null) {
				foreach (MultiplayerPlayerController player in connectedPlayers) {
					playerNames += player.username + "\n";
					numPlayers++;
				}
			}
		}

		public void OnGUI() {
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
			}
		}
	}
}
