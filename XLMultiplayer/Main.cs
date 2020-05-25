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

namespace XLMultiplayer {
	class Main {
		public static bool enabled;
		public static String modId;
		public static UnityModManager.ModEntry modEntry;

		public static HarmonyInstance harmonyInstance;

		public static MultiplayerMenu menu;
		public static MultiplayerUtilityMenu utilityMenu;
		public static MultiplayerController multiplayerController;

		private static ModUIBox uiBox;

		static void Load(UnityModManager.ModEntry modEntry) {
			Main.modEntry = modEntry;
			Main.modId = modEntry.Info.Id;

			modEntry.OnToggle = OnToggle;
		}

		static bool OnToggle(UnityModManager.ModEntry modEntry, bool value) {
			if (value == enabled) return true;
			enabled = value;

			if (enabled) {
				//Patch the replay editor
				harmonyInstance = HarmonyInstance.Create(modEntry.Info.Id);
				harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

				menu = ModMenu.Instance.gameObject.AddComponent<MultiplayerMenu>();
				utilityMenu = ModMenu.Instance.gameObject.AddComponent<MultiplayerUtilityMenu>();

				uiBox = ModMenu.Instance.RegisterModMaker("Silentbaws", "Silentbaws", 0);
				uiBox.AddCustom("Patreon", DisplayPatreon, () => enabled);

				MultiplayerUtils.StartMapLoading();
			} else {
				//Unpatch the replay editor
				harmonyInstance.UnpatchAll(harmonyInstance.Id);

				MultiplayerUtils.StopMapLoading();

				if (multiplayerController != null) multiplayerController.DisconnectFromServer();
				menu.CloseMultiplayerMenu();

				UnityEngine.Object.Destroy(menu);
				UnityEngine.Object.Destroy(utilityMenu);
			}

			return true;
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

	[HarmonyPatch(typeof(ReplayEditorController), "LoadFromFile")]
	static class LoadMultiplayerReplayPatch {
		static void Prefix(string path) {
			string multiplayerReplayFile = path + "\\" + Path.GetFileNameWithoutExtension(path);
			UnityModManager.Logger.Log("start load");
			using (MemoryStream ms = new MemoryStream(File.ReadAllBytes())) {
				UnityModManager.Logger.Log("start mem");
				if (CustomFileReader.HasSignature(ms, "SXLDF001")) {
					UnityModManager.Logger.Log("check read");
					using (CustomFileReader fileReader = new CustomFileReader(ms)) {
						UnityModManager.Logger.Log("start read");
						ReplayPlayerData playerData;
						PlayerDataInfo playerDataInfo;
						if (!fileReader.TryGetData<ReplayPlayerData, PlayerDataInfo>("player2", out playerData, out playerDataInfo)) {
							UnityModManager.Logger.Log("Huge surprise it wasn't found");
						} else {
							UnityModManager.Logger.Log("Found second player, wow you actually did something right! That's a first");
						}
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(SaveManager), "SaveReplay")]
	static class SaveMultiplayerReplayPatch {
		static void Postfix(string fileID, byte[] data) {
			byte[] result;
			MultiplayerRemotePlayerController remoteController = Main.multiplayerController.remoteControllers[0];
			List<ReplayRecordedFrame> recordedFrames = remoteController.recordedFrames;
			ReplayPlayerData replayData = new ReplayPlayerData(recordedFrames, new TransformInfo(ReplayRecorder.Instance.transform, Space.World), new List<GPEvent>(), null, null, null, null, null, ReplayEditorController.Instance.playbackController.characterCustomizer.CurrentCustomizations);
			using (MemoryStream memoryStream = new MemoryStream()) {
				using (CustomFileWriter customFileWriter = new CustomFileWriter(memoryStream, "SXLDF001")) {
					PlayerDataInfo dataInfo2 = new PlayerDataInfo("Player2");
					customFileWriter.AddData(replayData, "player2", dataInfo2);
					customFileWriter.Write();
					result = memoryStream.ToArray();
				}
			}

			string replaysPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SkaterXL";
			if (Directory.Exists(replaysPath) && Directory.Exists(replaysPath + "\\Replays")) {
				replaysPath += "\\Replays\\" + fileID;
				Directory.CreateDirectory(replaysPath);
				File.WriteAllBytes(replaysPath + "\\MultiplayerReplay", result);
			}
		}
	}

	[HarmonyPatch(typeof(ReplayEditor.ReplayPlaybackController), "SetPlaybackTime")]
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

					yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(ReplayEditor.ReplayPlaybackController), "ClipFrames").GetMethod);

					yield return new CodeInstruction(OpCodes.Ldc_I4_0);

					yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(List<ReplayEditor.ReplayRecordedFrame>), "Item").GetMethod);

					yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ReplayEditor.ReplayRecordedFrame), "time"));
				} else {
					yield return instruction;
				}
			}
		}
	}
}