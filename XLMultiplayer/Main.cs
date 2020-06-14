using System;
using UnityEngine;
using UnityModManagerNet;
using Harmony12;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Reflection;
using XLShredLib.UI;
using XLShredLib;
using System.IO;
using ReplayEditor;
using System.Linq;
using XLMultiplayerUI;
using Newtonsoft.Json;
using UnityEngine.UI;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System.Collections;

// TODO: Test with more players
// TODO: Make replays save audio
// TODO: Volume control for remote clients

namespace XLMultiplayer {
	class Server {
		public string name;
		public string ip;
		public string port;
		public string version;
		public string mapName;
		public int playerMax;
		public int playerCurrent;
	}
	
	public class PreviousUsername {
		[JsonProperty("Previous_Name")]
		public string username = "Username";
	}

	class Main {
		public static bool enabled;
		public static String modId;
		public static UnityModManager.ModEntry modEntry;

		public static HarmonyInstance harmonyInstance;
		
		public static MultiplayerUtilityMenu utilityMenu;
		public static MultiplayerController multiplayerController;

		private static ModUIBox uiBox;

		public static List<MultiplayerRemotePlayerController> remoteReplayControllers = new List<MultiplayerRemotePlayerController>();

		public static StreamWriter debugWriter;

		public static AssetBundle uiBundle;

		static void Load(UnityModManager.ModEntry modEntry) {
			Main.modEntry = modEntry;
			Main.modId = modEntry.Info.Id;

			modEntry.OnToggle = OnToggle;
		}

		static bool OnToggle(UnityModManager.ModEntry modEntry, bool value) {
			if (value == enabled) return true;
			enabled = value;

			if (enabled) {
				string directory = Directory.GetCurrentDirectory();
				try {
					File.Copy(modEntry.Path + "GameNetworkingSockets.dll", directory + "\\GameNetworkingSockets.dll", true);
					File.Copy(modEntry.Path + "libprotobuf.dll", directory + "\\libprotobuf.dll", true);
					File.Copy(modEntry.Path + "libcrypto-1_1-x64.dll", directory + "\\libcrypto-1_1-x64.dll", true);
					File.Copy(modEntry.Path + "System.Buffers.dll", directory + "\\System.Buffers.dll", true);
					File.Copy(modEntry.Path + "System.Memory.dll", directory + "\\System.Memory.dll", true);
					File.Copy(modEntry.Path + "System.Numerics.Vectors.dll", directory + "\\System.Numerics.Vectors.dll", true);
					File.Copy(modEntry.Path + "System.Runtime.CompilerServices.Unsafe.dll", directory + "\\System.Runtime.CompilerServices.Unsafe.dll", true);
				} catch (Exception) { }

				//Patch the replay editor
				harmonyInstance = HarmonyInstance.Create(modEntry.Info.Id);
				harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
				
				utilityMenu = ModMenu.Instance.gameObject.AddComponent<MultiplayerUtilityMenu>();

				uiBox = ModMenu.Instance.RegisterModMaker("Silentbaws", "Silentbaws", 0);
				uiBox.AddCustom("Patreon", DisplayPatreon, () => enabled);
				
				if (NewMultiplayerMenu.Instance == null) {
					if (uiBundle == null) uiBundle = AssetBundle.LoadFromFile(modEntry.Path + "multiplayerui");
					
					GameObject newMenuObject = GameObject.Instantiate(uiBundle.LoadAsset<GameObject>("Assets/Prefabs/Main Multiplayer Menu.prefab"));
					NewMultiplayerMenu.Instance.UpdateCallback = MenuUpdate;
					NewMultiplayerMenu.Instance.OnClickConnectCallback = Main.OnClickConnect;
					NewMultiplayerMenu.Instance.OnClickDisconnectCallback = Main.OnClickDisconnect;

					GameObject.DontDestroyOnLoad(newMenuObject);

					NewMultiplayerMenu.Instance.StartCoroutine(Main.StartUpdatingServerList());
				}

				MultiplayerUtils.StartMapLoading();
			} else {
				//Unpatch the replay editor
				harmonyInstance.UnpatchAll(harmonyInstance.Id);

				MultiplayerUtils.StopMapLoading();

				if (multiplayerController != null) multiplayerController.DisconnectFromServer();

				GameObject.Destroy(NewMultiplayerMenu.Instance.gameObject);
				UnityEngine.Object.Destroy(utilityMenu);
			}

			return true;
		}
		
		public static IEnumerator StartUpdatingServerList() {
			while (true) {
				if (NewMultiplayerMenu.Instance != null && NewMultiplayerMenu.Instance.serverBrowserMenu.activeSelf) {
					UnityWebRequest www = UnityWebRequest.Get("http://www.davisellwood.com/api/getservers/");
					yield return www.SendWebRequest();

					if (www.isNetworkError || www.isHttpError) {
						yield return new WaitForSeconds(5);
					} else {
						foreach (RectTransform trans in NewMultiplayerMenu.Instance.serverItems) {
							GameObject.Destroy(trans.gameObject);
						}

						yield return new WaitForEndOfFrame();

						var responseString = www.downloadHandler.text;
						responseString = responseString.Remove(0, 1).Remove(responseString.Length - 2, 1).Replace("\\\"", "\"");

						JArray a = JArray.Parse(responseString);

						while(NewMultiplayerMenu.Instance.serverItems.Count > 0) {
							bool allDestroyed = true;
							foreach(RectTransform trans in NewMultiplayerMenu.Instance.serverItems) {
								if (trans != null) {
									allDestroyed = false;
									break;
								}
							}

							if (allDestroyed) {
								NewMultiplayerMenu.Instance.serverItems.Clear();
								break;
							} else {
								yield return new WaitForEndOfFrame();
							}
						}

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
									NewMultiplayerMenu.Instance.AddServerItem(newServer.ip, newServer.port, newServer.name, newServer.mapName, newServer.version, $"{newServer.playerCurrent} / {newServer.playerMax}", ClickServerItem);
								}
							}
						}
						yield return new WaitForSeconds(30);
					}
				} else {
					yield return new WaitForSeconds(1);
				}
			}
		}

		private static void ClickServerItem(ServerListItem target) {
			JoinServer(target.ipAddress, target.port, NewMultiplayerMenu.Instance.usernameFields[0].text);
		}

		private static void OnClickConnect() {
			JoinServer(NewMultiplayerMenu.Instance.connectMenu.transform.Find("IP Address").GetComponent<InputField>().text, NewMultiplayerMenu.Instance.connectMenu.transform.Find("Port").GetComponent<InputField>().text, NewMultiplayerMenu.Instance.usernameFields[1].text);
		}

		private static void JoinServer(string ip, string port, string username) {
			if (Main.multiplayerController == null) {
				NewMultiplayerMenu.Instance.GetComponent<Canvas>().enabled = false;
				ModMenu.Instance.HideCursor(Main.modId);
				Main.multiplayerController =  ModMenu.Instance.gameObject.AddComponent<MultiplayerController>();

				username = username.Trim().Equals("") ? "Username" : username;

				PreviousUsername previousUsername = new PreviousUsername();
				if (File.Exists(Main.modEntry.Path + "\\PreviousName.json")) {
					previousUsername = JsonConvert.DeserializeObject<PreviousUsername>(File.ReadAllText(Main.modEntry.Path + "\\PreviousName.json"));
				}
				previousUsername.username = username;

				string newFile = JsonConvert.SerializeObject(previousUsername);
				File.WriteAllText(Main.modEntry.Path + "\\PreviousName.json", newFile);

				Main.multiplayerController.ConnectToServer(ip.Equals("") ? "127.0.0.1" : ip, (ushort)(port.Equals("") ? 7777 : int.Parse(port)), username);
			}
		}

		public static void OnClickDisconnect() {
			if (Main.multiplayerController != null) {
				GameObject.Destroy(Main.multiplayerController);
				NewMultiplayerMenu.Instance.GetComponent<Canvas>().enabled = false;
				ModMenu.Instance.HideCursor(Main.modId);
			}
		}

		// TODO: Move this to another file
		private static void MenuUpdate() {
			if (NewMultiplayerMenu.Instance != null) {
				if (Input.GetKeyDown(KeyCode.P)) {
					NewMultiplayerMenu.Instance.serverBrowserMenu.SetActive(false);
					NewMultiplayerMenu.Instance.connectMenu.SetActive(false);

					NewMultiplayerMenu.Instance.GetComponent<Canvas>().enabled = !NewMultiplayerMenu.Instance.GetComponent<Canvas>().enabled;

					if (NewMultiplayerMenu.Instance.GetComponent<Canvas>().enabled) {
						ModMenu.Instance.ShowCursor(Main.modId);
						if (Main.multiplayerController != null) {
							NewMultiplayerMenu.Instance.directConnectButton.SetActive(false);
							NewMultiplayerMenu.Instance.serverBrowserButton.SetActive(false);
							NewMultiplayerMenu.Instance.disconnectButton.SetActive(true);
						} else {
							NewMultiplayerMenu.Instance.directConnectButton.SetActive(true);
							NewMultiplayerMenu.Instance.serverBrowserButton.SetActive(true);
							NewMultiplayerMenu.Instance.disconnectButton.SetActive(false);
							
							PreviousUsername previousUsername = new PreviousUsername();

							if (File.Exists(Main.modEntry.Path + "\\PreviousName.json")) {
								previousUsername = JsonConvert.DeserializeObject<PreviousUsername>(File.ReadAllText(Main.modEntry.Path + "\\PreviousName.json"));
							}

							if (previousUsername.username != "") {
								foreach (InputField usernameField in NewMultiplayerMenu.Instance.usernameFields) {
									usernameField.text = previousUsername.username;
								}
							}
						}
					} else {
						ModMenu.Instance.HideCursor(Main.modId);
					}
				}
			}
		}

		static GUIStyle patreonStyle = null;
		static Texture2D patreonButton = null;
		static GUILayoutOption[] patreonOptions = null;

		private static void DisplayPatreon() {
			if (patreonStyle == null) {
				patreonStyle = new GUIStyle();
				patreonStyle.richText = true;
				patreonStyle.normal.textColor = Color.yellow;
				patreonStyle.alignment = TextAnchor.UpperCenter;
			}
			if (patreonButton == null) {
				patreonButton = new Texture2D(0, 0, TextureFormat.RGBA32, false);
				patreonButton.LoadImage(File.ReadAllBytes(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\patreon_button.png"));
				patreonButton.filterMode = FilterMode.Trilinear;
			}
			if (patreonOptions == null) {
				patreonOptions = new GUILayoutOption[] { GUILayout.Width(233), GUILayout.Height(54) };
			}

			patreonStyle.padding.bottom = -20;

			GUILayout.Label("Reserve your username and <b><i><color=#f00>a</color><color=#ff7f00>d</color><color=#ff0>d</color> <color=#0f0>s</color><color=#0ff>o</color><color=#00f>m</color><color=#8b00ff>e</color> <color=#f00>f</color><color=#ff7f00>l</color><color=#ff0>a</color><color=#0f0>i</color><color=#0ff>r</color></i></b> by supporting me on patreon\n\n", patreonStyle);

			patreonStyle.padding.bottom = 20;

			GUILayout.FlexibleSpace();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			
			if (GUILayout.Button(patreonButton, patreonStyle, patreonOptions)) {
				Application.OpenURL("https://www.patreon.com/silentbaws");
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
		}
	}

	[HarmonyPatch(typeof(ReplayEditorController), "OnDisable")]
	static class MultiplayReplayDisablePatch {
		static void Prefix() {
			if (Main.multiplayerController != null) {
				foreach (MultiplayerRemotePlayerController controller in Main.multiplayerController.remoteControllers) {
					if (controller.playerID != 255) {
						controller.skater.SetActive(true);
						controller.player.SetActive(true);
						controller.usernameObject.SetActive(true);
					}
				}
			} else {
				Main.debugWriter.Flush();
				Main.debugWriter.Close();
			}

			foreach (MultiplayerRemotePlayerController controller in Main.remoteReplayControllers) {
				controller.Destroy();
			}
			Main.remoteReplayControllers.Clear();
		}
	}

	[HarmonyPatch(typeof(ReplayEditorController), "Update")]
	static class MultiplayReplayUpdatePatch {
		static void Postfix(ReplayEditorController __instance) {
			foreach (MultiplayerRemotePlayerController controller in Main.remoteReplayControllers) {
				controller.replayController.TimeScale = ReplayEditorController.Instance.playbackController.TimeScale;
				controller.replayController.SetPlaybackTime(ReplayEditorController.Instance.playbackController.CurrentTime);
			}
		}
	}

	[HarmonyPatch(typeof(ReplayEditorController), "LoadFromFile")]
	static class LoadMultiplayerReplayPatch {
		static void Prefix(string path) {
			string multiplayerReplayFile = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + "\\";
			if (Directory.Exists(multiplayerReplayFile)) {
				if (Main.multiplayerController != null && Main.multiplayerController.debugWriter != null) Main.debugWriter = Main.multiplayerController.debugWriter;
				else Main.debugWriter = new StreamWriter("Multiplayer Replay DebugWriter.txt");
				using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(multiplayerReplayFile + "MultiplayerReplay.replay"))) {
					if (CustomFileReader.HasSignature(ms, "SXLDF001")) {
						using (CustomFileReader fileReader = new CustomFileReader(ms)) {
							ReplayPlayerData playerData = null;
							PlayerDataInfo playerDataInfo = null;
							int i = 0;
							do {
								if (fileReader.TryGetData<ReplayPlayerData, PlayerDataInfo>("player" + i.ToString(), out playerData, out playerDataInfo)) {
									Main.remoteReplayControllers.Add(new MultiplayerRemotePlayerController(Main.debugWriter));
									Main.remoteReplayControllers[i].ConstructPlayer();
									Main.remoteReplayControllers[i].characterCustomizer.LoadCustomizations(playerData.customizations);
									List<ReplayRecordedFrame> recordedFrames = new List<ReplayRecordedFrame>(playerData.recordedFrames);
									Main.remoteReplayControllers[i].recordedFrames = recordedFrames;
									Main.remoteReplayControllers[i].FinalizeReplay(false);
								}
								i++;
							} while (playerData != null);
						}
					}
				}
			}

			if (Main.multiplayerController != null) {
				foreach (MultiplayerRemotePlayerController controller in Main.multiplayerController.remoteControllers) {
					controller.skater.SetActive(false);
					controller.player.SetActive(false);
					controller.usernameObject.SetActive(false);
				}
			}
		}
	}

	[HarmonyPatch(typeof(SaveManager), "SaveReplay")]
	static class SaveMultiplayerReplayPatch {
		static void Postfix(string fileID, byte[] data) {
			string replaysPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SkaterXL";
			if (!Directory.Exists(replaysPath)) {
				Directory.CreateDirectory(replaysPath);
			}
			if (!Directory.Exists(replaysPath + "\\Replays")) {
				Directory.CreateDirectory(replaysPath + "\\Replays");
			}
			replaysPath += "\\Replays\\" + fileID;
			Directory.CreateDirectory(replaysPath);

			// TODO: Thread this shit, parallel as fuck boi
			// TODO: Refactor, I'm reusing lots of code that doesn't need to be repeated

			if (Main.remoteReplayControllers.Count > 0) {
				for (int i = 0; i < Main.remoteReplayControllers.Count; i++) {
					MultiplayerRemotePlayerController remoteController = Main.remoteReplayControllers[i];

					foreach (ReplayRecordedFrame frame in remoteController.replayController.ClipFrames) {
						frame.time -= ReplayEditorController.Instance.playbackController.ClipFrames[0].time;
					}

					foreach (ClothingGearObjet clothingPiece in remoteController.gearList) {
						if (clothingPiece.gearInfo.isCustom) {
							foreach (TextureChange change in clothingPiece.gearInfo.textureChanges) {
								if (change.textureID.ToLower().Equals("albedo")) {
									string newPath = replaysPath + "\\" + Path.GetFileName(change.texturePath);
									File.Copy(change.texturePath, newPath);
									change.texturePath = newPath;
								}
							}
						}
					}

					foreach (BoardGearObject boardPiece in remoteController.boardGearList) {
						if (boardPiece.gearInfo.isCustom) {
							foreach (TextureChange change in boardPiece.gearInfo.textureChanges) {
								if (change.textureID.ToLower().Equals("albedo")) {
									string newPath = replaysPath + "\\" + Path.GetFileName(change.texturePath);
									File.Copy(change.texturePath, newPath);
									change.texturePath = newPath;
								}
							}
						}
					}

					if (remoteController.currentBody.gearInfo.isCustom) {
						foreach (MaterialChange matChange in remoteController.currentBody.gearInfo.materialChanges) {
							foreach (TextureChange change in matChange.textureChanges) {
								if (change.textureID.ToLower().Equals("albedo")) {
									string newPath = replaysPath + "\\" + Path.GetFileName(change.texturePath);
									File.Copy(change.texturePath, newPath);
									change.texturePath = newPath;
								}
							}
						}
					}
				}

				byte[] result;

				using (MemoryStream memoryStream = new MemoryStream()) {
					using (CustomFileWriter customFileWriter = new CustomFileWriter(memoryStream, "SXLDF001")) {
						for (int i = 0; i < Main.remoteReplayControllers.Count; i++) {
							if (Main.remoteReplayControllers[i].replayController.ClipFrames.Count > 0) {
								List<ReplayRecordedFrame> recordedFrames = Main.remoteReplayControllers[i].replayController.ClipFrames;
								ReplayPlayerData replayData = new ReplayPlayerData(recordedFrames.ToArray(), new List<GPEvent>().ToArray(), null, null, null, null, null, null, null, null, null, null, Main.remoteReplayControllers[i].characterCustomizer.CurrentCustomizations);
								PlayerDataInfo dataInfo2 = new PlayerDataInfo("Player" + i.ToString());
								replayData.customizations = Main.remoteReplayControllers[i].characterCustomizer.CurrentCustomizations;
								customFileWriter.AddData(replayData, "player" + i.ToString(), dataInfo2);
								customFileWriter.Write();
							}
						}
						result = memoryStream.ToArray();
					}
				}

				File.WriteAllBytes(replaysPath + "\\MultiplayerReplay.replay", result);
			} else if (Main.multiplayerController != null && Main.remoteReplayControllers.Count == 0) {
				for (int i = 0; i < Main.multiplayerController.remoteControllers.Count; i++) {
					MultiplayerRemotePlayerController remoteController = Main.multiplayerController.remoteControllers[i];

					foreach (ReplayRecordedFrame frame in remoteController.replayController.ClipFrames) {
						frame.time -= ReplayEditorController.Instance.playbackController.ClipFrames[0].time;
					}

					foreach (ClothingGearObjet clothingPiece in remoteController.gearList) {
						if (clothingPiece.gearInfo.isCustom) {
							foreach (TextureChange change in clothingPiece.gearInfo.textureChanges) {
								if (change.textureID.ToLower().Equals("albedo")) {
									string newPath = replaysPath + "\\" + Path.GetFileName(change.texturePath);
									File.Copy(change.texturePath, newPath);
									change.texturePath = newPath;
								}
							}
						}
					}

					foreach (BoardGearObject boardPiece in remoteController.boardGearList) {
						if (boardPiece.gearInfo.isCustom) {
							foreach (TextureChange change in boardPiece.gearInfo.textureChanges) {
								if (change.textureID.ToLower().Equals("albedo")) {
									string newPath = replaysPath + "\\" + Path.GetFileName(change.texturePath);
									File.Copy(change.texturePath, newPath);
									change.texturePath = newPath;
								}
							}
						}
					}

					if (remoteController.currentBody.gearInfo.isCustom) {
						foreach (MaterialChange matChange in remoteController.currentBody.gearInfo.materialChanges) {
							foreach (TextureChange change in matChange.textureChanges) {
								if (change.textureID.ToLower().Equals("albedo")) {
									string newPath = replaysPath + "\\" + Path.GetFileName(change.texturePath);
									File.Copy(change.texturePath, newPath);
									change.texturePath = newPath;
								}
							}
						}
					}
				}

				byte[] result;

				using (MemoryStream memoryStream = new MemoryStream()) {
					using (CustomFileWriter customFileWriter = new CustomFileWriter(memoryStream, "SXLDF001")) {
						for (int i = 0; i < Main.multiplayerController.remoteControllers.Count; i++) {
							if(Main.multiplayerController.remoteControllers[i].replayController.ClipFrames.Count > 0) {
								List<ReplayRecordedFrame> recordedFrames = Main.multiplayerController.remoteControllers[i].replayController.ClipFrames;
								ReplayPlayerData replayData = new ReplayPlayerData(recordedFrames.ToArray(), new List<GPEvent>().ToArray(), null, null, null, null, null, null, null, null, null, null, Main.multiplayerController.remoteControllers[i].characterCustomizer.CurrentCustomizations);
								PlayerDataInfo dataInfo2 = new PlayerDataInfo("Player"+i.ToString());
								replayData.customizations = Main.multiplayerController.remoteControllers[i].characterCustomizer.CurrentCustomizations;
								customFileWriter.AddData(replayData, "player"+i.ToString(), dataInfo2);
								customFileWriter.Write();
							}
						}
						result = memoryStream.ToArray();
					}
				}
				
				File.WriteAllBytes(replaysPath + "\\MultiplayerReplay.replay", result);
			}
		}
	}

	[HarmonyPatch(typeof(ReplayPlaybackController), "CutClip")]
	static class CutClipMultiplayerReplayPatch {
		static void Prefix(ReplayPlaybackController __instance, float newStartTime, float newEndTime) {
			if (Main.remoteReplayControllers.Find(c => c.replayController == __instance) != null || (Main.multiplayerController != null && Main.multiplayerController.remoteControllers.Find(c => c.replayController == __instance) != null)) return;

			if (Main.multiplayerController != null) {
				foreach (MultiplayerRemotePlayerController controller in Main.multiplayerController.remoteControllers) {
					if(controller.replayController == null || controller.replayController.ClipFrames == null) {
						continue;
					}else if (controller.replayController.ClipFrames.Count > 0 && (controller.replayController.ClipFrames.Last().time <= newStartTime || controller.replayController.ClipFrames.First().time >= newEndTime)) {
						controller.replayController.ClipFrames.Clear();
					} else if(controller.replayController != null && controller.replayController.ClipFrames != null && controller.replayController.ClipFrames.Count > 0) {
						int framesToRemove = 0;
						while (framesToRemove < controller.replayController.ClipFrames.Count && controller.replayController.ClipFrames[framesToRemove].time < newStartTime) {
							framesToRemove++;
						}
						controller.replayController.ClipFrames.RemoveRange(0, framesToRemove);

						if(controller.replayController.ClipFrames.Count > 0) {
							framesToRemove = 0;
							while (framesToRemove < controller.replayController.ClipFrames.Count && controller.replayController.ClipFrames[controller.replayController.ClipFrames.Count - 1 - framesToRemove].time > newEndTime) {
								framesToRemove++;
							}
							controller.replayController.ClipFrames.RemoveRange(controller.replayController.ClipFrames.Count - framesToRemove, framesToRemove);
						}

						if(controller.replayController.ClipFrames.Count > 0) {
							controller.replayController.ClipFrames.ForEach(delegate (ReplayRecordedFrame f)
							{
								f.time -= newStartTime;
							});

							controller.replayController.ClipEndTime = newEndTime - newStartTime;
							controller.replayController.CurrentTime = Mathf.Clamp(controller.replayController.CurrentTime - newStartTime, 0f, controller.replayController.ClipEndTime);

							controller.replayController.StartCoroutine("UpdateAnimationClip");
						}
					}
				}
			}
			foreach (MultiplayerRemotePlayerController controller in Main.remoteReplayControllers) {
				if (controller.replayController == null || controller.replayController.ClipFrames == null) {
					continue;
				} else if(controller.replayController.ClipFrames.Count > 0 && (controller.replayController.ClipFrames.Last().time <= newStartTime || controller.replayController.ClipFrames.First().time >= newEndTime)) {
					Main.multiplayerController.remoteControllers.Remove(controller);
					controller.Destroy();
				} else if (controller.replayController != null && controller.replayController.ClipFrames != null && controller.replayController.ClipFrames.Count > 0) {
					int framesToRemove = 0;
					while (framesToRemove < controller.replayController.ClipFrames.Count && controller.replayController.ClipFrames[framesToRemove].time < newStartTime) {
						framesToRemove++;
					}
					controller.replayController.ClipFrames.RemoveRange(0, framesToRemove);

					if (controller.replayController.ClipFrames.Count > 0) {
						framesToRemove = 0;
						while (framesToRemove < controller.replayController.ClipFrames.Count && controller.replayController.ClipFrames[controller.replayController.ClipFrames.Count - 1 - framesToRemove].time > newEndTime) {
							framesToRemove++;
						}
						controller.replayController.ClipFrames.RemoveRange(controller.replayController.ClipFrames.Count - framesToRemove, framesToRemove);
					}

					if (controller.replayController.ClipFrames.Count > 0) {
						controller.replayController.ClipFrames.ForEach(delegate (ReplayRecordedFrame f) {
							f.time -= newStartTime;
						});

						controller.replayController.ClipEndTime = newEndTime - newStartTime;
						controller.replayController.CurrentTime = Mathf.Clamp(controller.replayController.CurrentTime - newStartTime, 0f, controller.replayController.ClipEndTime);

						controller.replayController.StartCoroutine("UpdateAnimationClip");
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(ReplayPlaybackController), "SetPlaybackTime")]
	static class ReplaySetPlaybackTimePatch {

		/* REPLACE ldc.r4 0 with
			ldarg.0 NULL
			call System.Collections.Generic.List`1[ReplayEditor.ReplayRecordedFrame] get_ClipFrames()
			ldc.i4.0 NULL
			callvirt ReplayEditor.ReplayRecordedFrame get_Item(Int32)
			ldfld System.Single time
		 */

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			foreach (CodeInstruction instruction in instructions) {
				if (instruction.opcode == OpCodes.Ldc_R4) {
					//BIG SHOUTOUT BLENDERMF
					yield return new CodeInstruction(OpCodes.Ldarg_0);

					yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(ReplayPlaybackController), "ClipFrames").GetMethod);

					yield return new CodeInstruction(OpCodes.Ldc_I4_0);

					yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(List<ReplayRecordedFrame>), "Item").GetMethod);

					yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ReplayRecordedFrame), "time"));
				} else {
					yield return instruction;
				}
			}
		}
	}
}