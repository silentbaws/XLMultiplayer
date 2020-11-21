using UnityEngine;
using TMPro;

namespace XLMultiplayerUI {
	public class ServerListItem : MonoBehaviour {
		public TMP_Text ServerName, ServerMap, ServerVersion, ServerPlayers;

		public string ipAddress, port;
		
		public OnClickDelegate onClickCallback;

		public void CreateServerItem(string ip, string port, string name, string map, string version, string players, OnClickDelegate newCallback) {
			ServerName.text = name;
			ServerMap.text = map;
			ServerVersion.text = version;
			ServerPlayers.text = players;
			this.port = port;
			ipAddress = ip;
			onClickCallback = newCallback;
		}

		public void OnClick() {
			NewMultiplayerMenu.Instance.OnClickCloseServerBrowser();
			onClickCallback(this);
		}
	}

	public delegate void OnClickDelegate(ServerListItem targetServer);
}
