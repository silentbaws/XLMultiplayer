using System.Collections.Generic;
using UnityModManagerNet;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.Diagnostics;

namespace XLMultiplayer {
	public class ServerBrowser : MonoBehaviour {
		private readonly string mainServer = "http://www.davisellwood.com/api/getservers/";
		private List<Server> servers = new List<Server>();

		// GUI stuff
		public bool showUI { get; private set; }
		private bool setUp;
		private Rect windowRect = new Rect(300f, 50f, 300f, 400f);
		private GUIStyle windowStyle;
		private GUIStyle spoilerBtnStyle;
		private GUIStyle sliderStyle;
		private GUIStyle thumbStyle;
		private readonly Color windowColor = new Color(0.2f, 0.2f, 0.2f);
		private string separator;
		private GUIStyle separatorStyle;
		private Vector2 scrollPosition = new Vector2();
		private GUIStyle usingStyle;

		public Stopwatch closeTimer = new Stopwatch();

		private void Start() {
			log("Starting");
			StartCoroutine(StartUpdatingServerList());
			showUI = false;
		}

		private void Update() {
			if (closeTimer.ElapsedMilliseconds > 1000) {
				closeTimer.Stop();
			}
		}

		public IEnumerator StartUpdatingServerList() {
			log("Started requesting servers");

			while (true) {
				if (showUI) {
					log("Requesting servers");

					UnityWebRequest www = UnityWebRequest.Get(mainServer);
					yield return www.SendWebRequest();

					if (www.isNetworkError || www.isHttpError) {
						log($"Error getting servers: {www.error}");
					} else {
						var responseString = www.downloadHandler.text;
						responseString = responseString.Remove(0, 1).Remove(responseString.Length - 2, 1).Replace("\\\"", "\"");

						log($"response: '{responseString}'");

						JArray a = JArray.Parse(responseString);

						servers.Clear();
						foreach (JObject o in a.Children<JObject>()) {
							foreach (JProperty p in o.Properties()) {
								if (p.Name == "fields") {
									Server newServer = new Server();
									foreach (JObject o2 in p.Children<JObject>()) {
										foreach (JProperty p2 in o2.Properties()) {
											switch (p2.Name) {
												case "name":
													newServer.name = (string)p2.Value;
													break;
												case "IP":
													newServer.ip = (string)p2.Value;
													break;
												case "port":
													newServer.port = (string)p2.Value;
													break;
												case "version":
													newServer.version = (string)p2.Value;
													break;
												case "maxPlayers":
													newServer.playerMax = (int)p2.Value;
													break;
												case "currentPlayers":
													newServer.playerCurrent = (int)p2.Value;
													break;
												case "mapName":
													newServer.mapName = (string)p2.Value;
													break;
											}
										}
									}
									servers.Add(newServer);
								}
							}
						}
					}

					yield return new WaitForSeconds(30);
				} else {
					yield return new WaitForSeconds(1);
				}
			}
		}

		void RenderWindow(int windowID) {
			if (Event.current.type == EventType.Repaint) windowRect.height = 0;

			GUI.DragWindow(new Rect(0, 0, 10000, 20));

			scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(400), GUILayout.Height(400));
			{
				GUIStyle title = new GUIStyle();
				title.fontSize = 16;
				title.richText = true;
				title.alignment = TextAnchor.UpperCenter;
				title.normal.textColor = Color.white;

				GUIStyle center = new GUIStyle();
				center.fontSize = 12;
				center.alignment = TextAnchor.MiddleCenter;
				center.normal.textColor = Color.white;

				foreach (var s in servers) {
					Label($"<b>{s.name}  v{s.version}</b>\n", title);
					Label($"<b>IP:</b> {s.ip}      <b>port:</b> {s.port}\n<b>map:</b> {s.mapName}      <b>players:</b> {s.playerCurrent}/{s.playerMax}", center);
					if (Button($"Connect with username \"{Main.menu.username}\"")) {
						ushort usedPort = 0;
						if (ushort.TryParse(s.port, out usedPort)) {
							this.Close();
							Main.menu.CreateMultiplayerManager();

							Main.multiplayerController.ConnectToServer(s.ip, usedPort, Main.menu.username);
						}
					}
					Separator();
				}

				GUILayout.FlexibleSpace();
				Separator();
				// Preset selection, save, close
				{
					BeginHorizontal();
					if (Button("Close")) {
						Close();
					}
					EndHorizontal();
				}
			}
			GUILayout.EndScrollView();
		}

		private class Server {
			public string name;
			public string ip;
			public string port;
			public string version;
			public string mapName;
			public int playerMax;
			public int playerCurrent;
		}

		#region Utility

		public void Open() {
			showUI = true;
		}

		public void Close() {
			showUI = false;
			closeTimer.Restart();
		}

		private void OnGUI() {
			if (!setUp) {
				setUp = true;
				SetUp();
			}

			GUI.backgroundColor = windowColor;

			if (showUI) {
				windowRect = GUILayout.Window(GUIUtility.GetControlID(FocusType.Passive), windowRect, RenderWindow, "Server Browser", windowStyle, GUILayout.Width(400));
			}
		}

		internal void log(string s) {
			UnityModManager.Logger.Log("[Server Browser] " + s);
		}

		private void SetUp() {
			DontDestroyOnLoad(gameObject);

			windowStyle = new GUIStyle(GUI.skin.window) {
				padding = new RectOffset(10, 10, 25, 10),
				contentOffset = new Vector2(0, -23.0f)
			};

			spoilerBtnStyle = new GUIStyle(GUI.skin.button) {
				fixedWidth = 100,
			};

			sliderStyle = new GUIStyle(GUI.skin.horizontalSlider) {
				fixedWidth = 200
			};

			thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb) {

			};

			separatorStyle = new GUIStyle(GUI.skin.label) {

			};
			separatorStyle.normal.textColor = Color.red;
			separatorStyle.fontSize = 4;

			separator = new string('=', 196);

			usingStyle = new GUIStyle(GUI.skin.label);
			usingStyle.normal.textColor = Color.red;
			usingStyle.fontSize = 16;
		}

		private void Label(string text, GUIStyle style) {
			GUILayout.Label(text, style);
		}

		private void Label(string text) {
			GUILayout.Label(text);
		}

		private void Separator() {
			Label(separator, separatorStyle);
		}

		private bool Button(string text) {
			return GUILayout.Button(text, GUILayout.Height(30));
		}

		private bool Spoiler(string text) {
			return GUILayout.Button(text, spoilerBtnStyle);
		}

		private void BeginHorizontal() {
			GUILayout.BeginHorizontal();
		}

		private void EndHorizontal() {
			GUILayout.EndHorizontal();
		}

		private float Slider(string name, float current, float min, float max, bool horizontal = true) {
			if (horizontal) BeginHorizontal();
			Label(name + ": " + current.ToString("0.00"));
			float res = GUILayout.HorizontalSlider(current, min, max, sliderStyle, thumbStyle);
			if (horizontal) EndHorizontal();
			return res;
		}

		private int SliderInt(string name, int current, int min, int max, bool horizontal = true) {
			if (horizontal) BeginHorizontal();
			Label(name + ": " + current);
			float res = GUILayout.HorizontalSlider(current, min, max, sliderStyle, thumbStyle);
			if (horizontal) EndHorizontal();
			return Mathf.FloorToInt(res);
		}

		#endregion
	}
}
