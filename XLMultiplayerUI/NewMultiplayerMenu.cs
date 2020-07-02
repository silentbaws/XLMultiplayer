using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XLMultiplayerUI {
	public delegate void OnClickDisconnectDelegate();
	public delegate void OnClickConnectDelegate();
	public delegate void MenuUpdateDelegate();

	public class NewMultiplayerMenu : MonoBehaviour{
		public GameObject serverListItem;
		public GameObject serverListBox;
		public GameObject serverViewport;

		public GameObject serverBrowserMenu;
		public GameObject connectMenu;

		public GameObject serverBrowserButton;
		public GameObject directConnectButton;

		public GameObject disconnectButton;

		public InputField[] usernameFields;

		public InputField[] textFields;

		private Vector2 itemRealSize;

		public List<RectTransform> serverItems = new List<RectTransform>();

		public OnClickConnectDelegate OnClickConnectCallback;
		public OnClickDisconnectDelegate OnClickDisconnectCallback;
		public MenuUpdateDelegate UpdateCallback;

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
		}

		public bool IsFocusedInput() {
			bool result = false;
			foreach (InputField f in textFields) {
				result |= f.isFocused;
			}
			return result;
		}

		public void OnClickDirect() {
			if (!serverBrowserMenu.activeSelf && !connectMenu.activeSelf) connectMenu.SetActive(true);
		}

		public void OnClickServerBrowser() {
			if (!serverBrowserMenu.activeSelf && !connectMenu.activeSelf) serverBrowserMenu.SetActive(true);
		}

		public void OnClickCloseDirect() {
			connectMenu.SetActive(false);
		}

		public void OnClickCloseServerBrowser() {
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
			if (this.UpdateCallback != null) UpdateCallback();
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
