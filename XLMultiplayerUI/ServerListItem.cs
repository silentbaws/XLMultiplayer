using UnityEngine;
using UnityEngine.UI;

namespace XLMultiplayerUI {
	public class ServerListItem : MonoBehaviour {
		public Text ServerName, ServerMap, ServerVersion, ServerPlayers;

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
			onClickCallback(this);
		}
	}

	public delegate void OnClickDelegate(ServerListItem targetServer);
}
