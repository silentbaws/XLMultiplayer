using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XLMultiplayerUI {
	public delegate void OnClickDisconnectDelegate();
	public delegate void OnClickConnectDelegate();
	public delegate void MenuUpdateDelegate();
	public delegate void OnSaveVolume(float newVolume);
	public delegate void OnSendChatMessage(string message);

	public class NewMultiplayerMenu : MonoBehaviour {
		public GameObject serverListItem;
		public GameObject serverListBox;
		public GameObject serverViewport;

		public GameObject serverBrowserMenu;
		public GameObject connectMenu;

		public GameObject serverBrowserButton;
		public GameObject directConnectButton;

		public GameObject disconnectButton;

		public TMP_InputField VolumeInput;
		public Slider VolumeSlider;

		public TMP_InputField[] usernameFields;

		public TMP_InputField[] textFields;

		private Vector2 itemRealSize;

		public List<RectTransform> serverItems = new List<RectTransform>();

		public OnClickConnectDelegate OnClickConnectCallback;
		public OnClickDisconnectDelegate OnClickDisconnectCallback;
		public MenuUpdateDelegate UpdateCallback;
		public OnSaveVolume SaveVolume;
		public OnSendChatMessage SendChatMessage;

		public GameObject BlurQuad;
		public GameObject GameBlurQuad;

		public GameObject mainMenuObject;

		public TMP_InputField MessageInput;
		public TMP_Text MessageBox;

		public ScrollRect messageScrollBar;

		private float currentMessageBoxY = 0f;
		private float oldScroll = 0f;
		private Vector2 currentMessageBoxSize;

		private Transform mainCamera;

		private static NewMultiplayerMenu _instance;

		public static NewMultiplayerMenu Instance {
			get {
				return _instance;
			}
			private set {
				_instance = value;
			}
		}

		public void Awake() {
			if (Instance != null && Instance != this) {
				GameObject.Destroy(this);
			} else {
				Instance = this;
			}

			mainCamera = Camera.main.transform;

			currentMessageBoxSize = MessageBox.GetComponent<RectTransform>().sizeDelta;
		}

		public bool IsFocusedInput() {
			bool result = false;
			foreach (TMP_InputField f in textFields) {
				result |= (f != null && f.isFocused);
			}
			return result;
		}

		public void OnClickDirect() {
			if (!serverBrowserMenu.activeSelf && !connectMenu.activeSelf) connectMenu.SetActive(true);
		}

		public void OnClickServerBrowser() {
			if (GameBlurQuad == null) {
				GameBlurQuad = GameObject.Instantiate(BlurQuad);
			}

			if (!serverBrowserMenu.activeSelf && !connectMenu.activeSelf) serverBrowserMenu.SetActive(true);
		}

		public void OnClickCloseDirect() {
			connectMenu.SetActive(false);
		}

		public void OnClickCloseServerBrowser() {
			if (GameBlurQuad != null)
				GameObject.Destroy(GameBlurQuad);
			serverBrowserMenu.SetActive(false);
		}

		public void OnClickConnect() {
			OnClickCloseDirect();
			if (this.OnClickConnectCallback != null) OnClickConnectCallback();
		}

		public void OnClickDisconnect() {
			if (this.OnClickDisconnectCallback != null) OnClickDisconnectCallback();
		}

		public void Update() {
			if (GameBlurQuad != null) {
				if (mainCamera == null) {
					mainCamera = Camera.main.transform;
				}

				GameBlurQuad.transform.rotation = mainCamera.rotation;
				GameBlurQuad.transform.position = mainCamera.position;
				GameBlurQuad.transform.position += mainCamera.forward * Camera.main.nearClipPlane * 1.1f;
			}


			Vector2 newSize = MessageBox.GetComponent<RectTransform>().sizeDelta;
			if (newSize != currentMessageBoxSize) {
				MessageBoxChange(newSize);
			} else {
				currentMessageBoxY = MessageBox.transform.localPosition.y;
				oldScroll = messageScrollBar.verticalNormalizedPosition;
			}

			if (this.UpdateCallback != null) UpdateCallback();

			if (Input.GetKeyDown(KeyCode.A)) {
				AddServerItem("127.0.0.1", "7777", "WOW THIS IS A FUCKING COOL SERVER BUD", "THAT ONE SUPER POG MAP EVERYONE PLAYS NOW", "v0.11.3", "20/20", null);
			}
		}

		public void LateUpdate() {
			if (GameBlurQuad != null) {
				if (mainCamera == null) {
					mainCamera = Camera.main.transform;
				}

				GameBlurQuad.transform.rotation = mainCamera.rotation;
				GameBlurQuad.transform.position = mainCamera.position;
				GameBlurQuad.transform.position += mainCamera.forward * Camera.main.nearClipPlane * 1.1f;
			}
		}

		public void OnClickPatreon() {
			Application.OpenURL("https://www.patreon.com/silentbaws");
		}

		public void OnChangeVolumeSlider() {
			if (!VolumeInput.isFocused) {
				VolumeInput.text = VolumeSlider.value.ToString("0.000");
				SaveVolume(float.Parse(VolumeInput.text));
			}
		}

		public void OnChangeVolumeText() {
			float newVolume = 0;
			if (float.TryParse(VolumeInput.text, out newVolume)) {
				VolumeSlider.value = newVolume;
			}
		}

		public void MessageBoxChange(Vector2 newSize) {
			if (oldScroll <= 0.001f)
				messageScrollBar.verticalNormalizedPosition = 0f;
			else
				MessageBox.transform.localPosition = new Vector3(MessageBox.transform.localPosition.x, -newSize.y/2 - (-currentMessageBoxSize.y/2 - currentMessageBoxY), MessageBox.transform.localPosition.z);

			currentMessageBoxSize = newSize;
		}

		public void OnEndEdit() {
			if (Input.GetKeyDown(KeyCode.Return)) {
				SendChatMessage?.Invoke(MessageInput.text);

				MessageInput.interactable = false;

				MessageInput.text = "";
			}
		}

		public void AddServerItem(string ip, string port, string name, string map, string version, string players, OnClickDelegate clickFunction) {
			GameObject newItem = GameObject.Instantiate(serverListItem, serverViewport.transform);
			RectTransform trans = newItem.GetComponent<RectTransform>();

			ServerListItem listItem = newItem.GetComponent<ServerListItem>();
			if (listItem != null) {
				listItem.CreateServerItem(ip, port, name, map, version, players, clickFunction);
			}

			itemRealSize = new Vector2(trans.rect.width, trans.rect.height);

			int numChildren = 0;
			foreach (Transform t in serverListBox.transform) {
				if (t != serverListBox.transform) {
					numChildren++;
				}
			}

			trans.anchorMin = new Vector2(trans.anchorMin.x, 1f);
			trans.anchorMax = new Vector2(trans.anchorMax.x, 1f);

			serverListBox.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemRealSize.y * (numChildren + 1));

			serverItems.Add(trans);

			newItem.transform.SetParent(serverListBox.transform);

			trans.offsetMax = new Vector2(trans.offsetMax.x, -itemRealSize.y * numChildren);

			foreach (RectTransform tran in serverItems) {
				tran.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemRealSize.y);
			}
		}
	}
}
