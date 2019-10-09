using System.Collections.Generic;
using UnityModManagerNet;
using XLShredLib;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace XLMultiplayer
{

    public class ServerBrowser : MonoBehaviour
    {

        private readonly string mainServer = "http://www.davisellwood.com/api/getservers/";
        private List<Server> servers = new List<Server>();

        // GUI stuff
        private bool showUI = false;
        private GameObject master;
        private bool setUp;
        private Rect windowRect = new Rect(300f, 50f, 600f, 0f);
        private GUIStyle windowStyle;
        private GUIStyle spoilerBtnStyle;
        private GUIStyle sliderStyle;
        private GUIStyle thumbStyle;
        private readonly Color windowColor = new Color(0.2f, 0.2f, 0.2f);
        private string separator;
        private GUIStyle separatorStyle;
        private Vector2 scrollPosition = new Vector2();
        private GUIStyle usingStyle;

        private void Start() {
            log("Starting");
            StartCoroutine(StartUpdatingServerList());
        }

        private void Update() {
        }

        public IEnumerator StartUpdatingServerList() {
            log("started Requesting");

            while (true) {
                log("Requesting");

                UnityWebRequest www = UnityWebRequest.Get(mainServer);
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError) {
                    log($"Error getting servers: {www.error}");
                }
                else {
                    var responseString = www.downloadHandler.text;
                    log($"response: '{responseString}'");

                    JArray a = JArray.Parse(responseString.Replace("\\\"", ""));

                    foreach (JObject o in a.Children<JObject>()) {
                        foreach (JProperty p in o.Properties()) {
                            string name = p.Name;
                            //string value = (string)p.Value;
                            log(name);// + " -- " + value);
                        }
                    }

                    servers.Clear();
                    //foreach (var j in json.Children()) {
                    //    log($"Child: {j.ToString()}");
                    //    var s = j.ToObject<Server>();
                    //    servers.Add(s);
                    //}
                }

                yield return new WaitForSeconds(30);
            }
        }

        void RenderWindow(int windowID) {
            if (Event.current.type == EventType.Repaint) windowRect.height = 0;

            GUI.DragWindow(new Rect(0, 0, 10000, 20));

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(400), GUILayout.Height(750));
            {
                foreach (var s in servers) {
                    //Label($"IP: {s.ip}, port: {s.port}");
                    Label($"IP: {s.pk}");
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

        private class Server
        {
            public string model;
            public int pk;
            //[DataMember]
            //public string fields;
            //[DataMember]
            //public string ip;
            //[DataMember]
            //public string port;
        }

        #region Utility

        public void Open() {
            showUI = true;
        }

        public void Close() {
            showUI = false;
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
            master = GameObject.Find("Master Prefab");
            if (master != null) {
                DontDestroyOnLoad(master);
            }

            windowStyle = new GUIStyle(GUI.skin.window) {
                padding = new RectOffset(10, 10, 25, 10),
                contentOffset = new Vector2(0, -23.0f)
            };

            spoilerBtnStyle = new GUIStyle(GUI.skin.button) {
                fixedWidth = 100
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

            separator = new string('_', 188);

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
            return GUILayout.Button(text);
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
