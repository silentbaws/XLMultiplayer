using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

		public GameObject blurQuad;
		public GameObject gameBlurQuad;

		public GameObject mainMenuObject;

		public GameObject chatCanvas;
		public GameObject chatObject;
		public GameObject chatMoveObject;

		public TMP_InputField messageInput;
		public TMP_Text messageBox;

		public ScrollRect messageScrollBar;

		private float currentMessageBoxY = 0f;
		private float oldScroll = 0f;
		private Vector2 currentMessageBoxSize;

		private Transform mainCamera;

		private static NewMultiplayerMenu _instance;

		private bool movingChat = false;
		private Vector2 mouseStartPosition = Vector2.zero;
		private Vector2 chatStartPosition = Vector2.zero;

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

			currentMessageBoxSize = messageBox.GetComponent<RectTransform>().sizeDelta;
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
			if (gameBlurQuad == null) {
				gameBlurQuad = GameObject.Instantiate(blurQuad);
			}

			if (!serverBrowserMenu.activeSelf && !connectMenu.activeSelf) serverBrowserMenu.SetActive(true);
		}

		public void OnClickCloseDirect() {
			connectMenu.SetActive(false);
		}

		public void OnClickCloseServerBrowser() {
			if (gameBlurQuad != null)
				GameObject.Destroy(gameBlurQuad);
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
			if (gameBlurQuad != null) {
				if (mainCamera == null) {
					mainCamera = Camera.main.transform;
				}

				gameBlurQuad.transform.rotation = mainCamera.rotation;
				gameBlurQuad.transform.position = mainCamera.position;
				gameBlurQuad.transform.position += mainCamera.forward * Camera.main.nearClipPlane * 1.1f;
			}

			if (Input.GetKeyUp(KeyCode.Mouse0) && movingChat) {
				movingChat = false;
			} else if (Input.GetKeyDown(KeyCode.Mouse0) && EventSystem.current.currentSelectedGameObject == chatMoveObject) {
				movingChat = true;

				Debug.Log("Moving");

				mouseStartPosition = Input.mousePosition;
				chatStartPosition = chatObject.transform.localPosition;
			}

			if (movingChat) {
				Debug.Log("Moving stuff");
				Vector2 mousePos = Input.mousePosition;
				chatObject.transform.localPosition = chatStartPosition - mouseStartPosition + mousePos;
			}


			Vector2 newSize = messageBox.GetComponent<RectTransform>().sizeDelta;
			if (newSize != currentMessageBoxSize) {
				MessageBoxChange(newSize);
			} else {
				currentMessageBoxY = messageBox.transform.localPosition.y;
				oldScroll = messageScrollBar.verticalNormalizedPosition;
			}

			if (this.UpdateCallback != null) UpdateCallback();

			if (Input.GetKeyDown(KeyCode.A)) {
				AddServerItem("127.0.0.1", "7777", "WOW THIS IS A FUCKING COOL SERVER BUD", "THAT ONE SUPER POG MAP EVERYONE PLAYS NOW", "v0.11.3", "20/20", null);
			}
		}

		public void LateUpdate() {
			if (gameBlurQuad != null) {
				if (mainCamera == null) {
					mainCamera = Camera.main.transform;
				}

				gameBlurQuad.transform.rotation = mainCamera.rotation;
				gameBlurQuad.transform.position = mainCamera.position;
				gameBlurQuad.transform.position += mainCamera.forward * Camera.main.nearClipPlane * 1.1f;
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
				messageBox.transform.localPosition = new Vector3(messageBox.transform.localPosition.x, -newSize.y/2 - (-currentMessageBoxSize.y/2 - currentMessageBoxY), messageBox.transform.localPosition.z);

			currentMessageBoxSize = newSize;
		}

		public void OnEndEdit() {
			if (Input.GetKeyDown(KeyCode.Return)) {
				SendChatMessage?.Invoke(messageInput.text);

				messageInput.interactable = false;

				messageInput.text = "";
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
