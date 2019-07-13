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

		private IEnumerator WaitForRequest(WWW www, bool loadConnectTexture) {
			yield return www;

			// check for errors
			if (www.error == null) {
				if (!loadConnectTexture) {
					this.disconnectedTexture = www.texture;
					this.disconnectedImage = new GameObject().AddComponent<Image>();
					this.disconnectedImage.sprite = Sprite.Create(disconnectedTexture, new Rect(0, 0, 300, 300), Vector2.zero);
					this.disconnectedImage.transform.SetParent(this.statusCanvas.transform, false);
					this.disconnectedImage.rectTransform.sizeDelta = new Vector2(52f, 52f);
					this.disconnectedImage.rectTransform.anchorMin = new Vector2(0.95f, 0.95f);
					this.disconnectedImage.rectTransform.anchorMax = new Vector2(0.975f, 0.975f);
					this.disconnectedImage.rectTransform.anchoredPosition = new Vector2(1f, 1f);
					AspectRatioFitter fitter = this.disconnectedImage.gameObject.AddComponent<AspectRatioFitter>();
					fitter.aspectRatio = 1.0f;
					fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
					this.disconnectedImage.color = Color.white;
				} else {
					this.connectedTexture = www.texture;
					this.connectedImage = new GameObject().AddComponent<Image>();
					this.connectedImage.sprite = Sprite.Create(connectedTexture, new Rect(0, 0, 300, 300), Vector2.zero);
					this.connectedImage.transform.SetParent(this.statusCanvas.transform, false);
					this.connectedImage.rectTransform.sizeDelta = new Vector2(52f, 52f);
					this.connectedImage.rectTransform.anchorMin = new Vector2(0.95f, 0.95f);
					this.connectedImage.rectTransform.anchorMax = new Vector2(0.975f, 0.975f);
					this.connectedImage.rectTransform.anchoredPosition = new Vector2(1f, 1f);
					AspectRatioFitter fitter = this.connectedImage.gameObject.AddComponent<AspectRatioFitter>();
					fitter.aspectRatio = 1.0f;
					fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
					this.disconnectedImage.color = Color.white;
				}
				www.Dispose();
			}
		}

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

			WWW www = new WWW(texturePath + "StatusGreen.png");
			StartCoroutine(WaitForRequest(www, true));

			www = new WWW(texturePath + "StatusRed.png");
			StartCoroutine(WaitForRequest(www, false));
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
				Rect rect = new Rect(Screen.width - Screen.width * 0.2f, Screen.width * 0.1f, Screen.width * 0.2f, (numPlayers + 1) * 25f);

				GUI.contentColor = Color.black;
				GUI.Label(rect, playerNames);
			}
		}
	}
}
