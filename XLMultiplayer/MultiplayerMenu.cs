using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XLShredLib;

namespace XLMultiplayer {
	// Token: 0x0200005D RID: 93
	public class MultiplayerMenu : MonoBehaviour {

		private void Start() {
		}

		private void Update() {
			if (Input.GetKeyDown(KeyCode.P)) {
				this.multiplayerMenuOpen = !this.multiplayerMenuOpen;
				if (this.multiplayerMenuOpen) {
					this.OpenMultiplayerMenu();
				} else {
					this.CloseMultiplayerMenu();
				}
			}
			if (this.multiplayerMenuConnectText != null) this.multiplayerMenuConnectText.text = this.multiplayerManager == null || !this.multiplayerManager.runningClient ? "Connect To Server" : "Disconnect";
			if (this.multiplayerMenuOpen) Cursor.visible = true;
		}

		public void EndMultiplayer() {
			if(this.multiplayerManager != null) {
				this.multiplayerManager.KillConnection();
				Destroy(this.multiplayerManager);
			}
			CloseMultiplayerMenu();
		}

		// Token: 0x06000448 RID: 1096 RVA: 0x0002BA4C File Offset: 0x00029C4C
		private void OpenMultiplayerMenu() {
			if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null) {
				GameObject gameObject = new GameObject("EVENTSYSTEMCUNTFUCK");
				gameObject.AddComponent<EventSystem>();
				gameObject.AddComponent<StandaloneInputModule>();
			}

			ipAddress = "IP Address";
			port = "Port";
			username = "Username";

			this.multiplayerMenu = new GameObject();
			this.multiplayerMenuCanvas = this.multiplayerMenu.AddComponent<Canvas>();
			this.multiplayerMenu.AddComponent<GraphicRaycaster>();

			CanvasScaler scaler = this.multiplayerMenu.AddComponent<CanvasScaler>();
			scaler.scaleFactor = 10f;
			scaler.referenceResolution = new Vector2(1920, 1080);
			scaler.referencePixelsPerUnit = 100;
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

			this.multiplayerMenuCanvas.renderMode = RenderMode.ScreenSpaceCamera;

			this.multiplayerMenuTextObject = new GameObject();
			this.multiplayerMenuTextObject.transform.SetParent(this.multiplayerMenu.transform, false);

			this.multiplayerMenuText = this.multiplayerMenuTextObject.AddComponent<Text>();
			this.multiplayerMenuText.supportRichText = true;
			this.multiplayerMenuText.text = "<b>Silent's Multiplayer Mod Menu</b>";
			this.multiplayerMenuText.transform.SetParent(this.multiplayerMenuCanvas.transform, false);
			this.multiplayerMenuText.rectTransform.sizeDelta = Vector2.zero;
			this.multiplayerMenuText.rectTransform.anchorMin = new Vector2(0f, 0.9f);
			this.multiplayerMenuText.rectTransform.anchorMax = new Vector2(0.3f, 1f);
			this.multiplayerMenuText.rectTransform.anchoredPosition = new Vector2(0.5f, 0.5f);
			this.multiplayerMenuText.color = Color.black;
			this.multiplayerMenuText.font = Resources.FindObjectsOfTypeAll<Font>()[0];
			this.multiplayerMenuText.fontSize = 35;
			this.multiplayerMenuText.alignment = TextAnchor.MiddleCenter;

			this.multiplayerMenuPaypalObject = new GameObject();
			this.multiplayerMenuPaypalObject.transform.SetParent(this.multiplayerMenuCanvas.transform, false);
			this.multiplayerMenuPaypalButtonImage = this.multiplayerMenuPaypalObject.AddComponent<Image>();
			this.multiplayerMenuPaypalButtonImage.transform.SetParent(this.multiplayerMenu.transform, false);
			this.multiplayerMenuPaypalButtonImage.rectTransform.sizeDelta = new Vector2(180, 50);
			this.multiplayerMenuPaypalButtonImage.rectTransform.anchorMin = new Vector2(0.05f, 0.75f);
			this.multiplayerMenuPaypalButtonImage.rectTransform.anchorMax = new Vector2(0.25f, 0.85f);
			Texture2D paypalTexture = new Texture2D(720, 200, TextureFormat.RGBA32, false);
			paypalTexture.LoadImage(File.ReadAllBytes(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Paypal.png"));
			paypalTexture.filterMode = FilterMode.Point;
			this.multiplayerMenuPaypalButtonImage.sprite = Sprite.Create(paypalTexture, new Rect(0, 0, 720, 200), Vector2.zero, 100);
			this.multiplayerMenuPaypalButtonImage.rectTransform.anchoredPosition = new Vector2(0.5f, 0.5f);
			this.multiplayerMenuPaypalButtonImage.color = Color.white;
			this.multiplayerMenuPaypalButton = this.multiplayerMenuPaypalObject.AddComponent<Button>();
			this.multiplayerMenuPaypalButton.targetGraphic = this.multiplayerMenuPaypalButtonImage;

			this.multiplayerMenuPaypalButton.onClick.AddListener(() => {
				Application.OpenURL("https://www.paypal.me/silentbaws");
			});

			this.multiplayerMenuConnectObject = new GameObject();
			this.multiplayerMenuConnectObject.transform.SetParent(this.multiplayerMenuCanvas.transform, false);
			this.multiplayerMenuConnectButtonImage = this.multiplayerMenuConnectObject.AddComponent<Image>();
			this.multiplayerMenuConnectButtonImage.transform.SetParent(this.multiplayerMenu.transform, false);
			this.multiplayerMenuConnectButtonImage.rectTransform.sizeDelta = new Vector2(180f, 50f);
			this.multiplayerMenuConnectButtonImage.rectTransform.anchorMin = new Vector2(0.05f, 0.6f);
			this.multiplayerMenuConnectButtonImage.rectTransform.anchorMax = new Vector2(0.25f, 0.7f);
			this.multiplayerMenuConnectButtonImage.rectTransform.anchoredPosition = new Vector2(0.5f, 0.5f);
			this.multiplayerMenuConnectButtonImage.color = Color.white;
			this.multiplayerMenuConnectButton = this.multiplayerMenuConnectObject.AddComponent<Button>();
			this.multiplayerMenuConnectButton.targetGraphic = this.multiplayerMenuConnectButtonImage;

			this.multiplayerMenuConnectButton.onClick.AddListener(delegate () {
				if (this.multiplayerManagerObject == null || !this.multiplayerManager.runningClient) {
					if (this.multiplayerManagerObject == null) {
						this.multiplayerManagerObject = new GameObject();
						this.multiplayerManagerObject.transform.parent = this.transform;
						this.multiplayerManager = this.multiplayerManagerObject.AddComponent<MultiplayerController>();
					}
					this.multiplayerManager.ConnectToServer(ipAddress.Equals("IP Address") ? "127.0.0.1" : ipAddress, port.Equals("Port") ? 7777 : int.Parse(port), this.username);
				} else if (this.multiplayerManagerObject != null) {
					this.multiplayerManager.KillConnection();
				}
			});

			this.multiplayerMenuConnectTextObject = new GameObject();
			this.multiplayerMenuConnectTextObject.transform.SetParent(this.multiplayerMenuConnectObject.transform, false);
			this.multiplayerMenuConnectText = this.multiplayerMenuConnectTextObject.AddComponent<Text>();
			this.multiplayerMenuConnectText.text = this.multiplayerManagerObject == null || !this.multiplayerManager.runningClient ? "Connect To Server" : "Disconnect";
			this.multiplayerMenuConnectText.transform.SetParent(this.multiplayerMenuCanvas.transform, false);
			this.multiplayerMenuConnectText.rectTransform.sizeDelta = Vector2.zero;
			this.multiplayerMenuConnectText.rectTransform.anchorMin = new Vector2(0.05f, 0.6f);
			this.multiplayerMenuConnectText.rectTransform.anchorMax = new Vector2(0.25f, 0.7f);
			this.multiplayerMenuConnectText.rectTransform.anchoredPosition = new Vector2(0.5f, 0.5f);
			this.multiplayerMenuConnectText.color = Color.black;
			this.multiplayerMenuConnectText.font = Resources.FindObjectsOfTypeAll<Font>()[0];
			this.multiplayerMenuConnectText.fontSize = 25;
			this.multiplayerMenuConnectText.alignment = TextAnchor.MiddleCenter;
			this.multiplayerMenuConnectText.raycastTarget = false;

			ModMenu.Instance.ShowCursor(Main.modId);

			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		// Token: 0x06000449 RID: 1097 RVA: 0x0000533E File Offset: 0x0000353E
		private void CloseMultiplayerMenu() {
			UnityEngine.Object.Destroy(this.multiplayerMenu);
			UnityEngine.Object.Destroy(this.multiplayerMenuTextObject);
			UnityEngine.Object.Destroy(this.multiplayerMenuConnectButton);

			ModMenu.Instance.HideCursor(Main.modId);

			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}

		private void OnGUI() {
			if (this.multiplayerMenuOpen && (this.multiplayerManagerObject == null || !this.multiplayerManager.runningClient)) {
				Vector3[] vectors = new Vector3[4];
				this.multiplayerMenuConnectButtonImage.rectTransform.GetWorldCorners(vectors);
				float screenScale = Screen.height / 1080f;
				Rect rect = new Rect(vectors[0].x, 1080 * screenScale - vectors[0].y + 5, (vectors[2].x - vectors[0].x) * 0.6f, 25f);
				Rect rect2 = new Rect(rect.width + rect.x + 5, 1080 * screenScale - vectors[0].y + 5, vectors[2].x - rect.width - rect.x - 5, 25f);
				ipAddress = GUI.TextField(rect, ipAddress, 16);
				port = GUI.TextField(rect2, port, 4);
				Rect rect3 = new Rect(rect.x, rect.y + rect.height + 5, rect.width, rect.height);
				username = GUI.TextField(rect3, username, 16);
			}
		}

		public void OnDestroy() {
			EndMultiplayer();
		}

		private bool multiplayerMenuOpen;

		// Token: 0x04000458 RID: 1112
		private GameObject multiplayerMenu;

		// Token: 0x04000459 RID: 1113
		private Canvas multiplayerMenuCanvas;

		// Token: 0x0400045A RID: 1114
		private GameObject multiplayerMenuTextObject;

		private GameObject multiplayerMenuPaypalObject;
		private Button multiplayerMenuPaypalButton;
		private Image multiplayerMenuPaypalButtonImage;

		// Token: 0x0400045B RID: 1115
		private Text multiplayerMenuText;

		// Token: 0x0400045C RID: 1116
		private GameObject multiplayerMenuConnectObject;

		// Token: 0x0400045D RID: 1117
		private Button multiplayerMenuConnectButton;

		private Text multiplayerMenuConnectText;
		private GameObject multiplayerMenuConnectTextObject;

		private string username;
		private string ipAddress;
		private string port;

		// Token: 0x04000460 RID: 1120
		private Image multiplayerMenuConnectButtonImage;

		// Token: 0x04000462 RID: 1122
		public GameObject multiplayerManagerObject;

		public MultiplayerController multiplayerManager;
	}
}