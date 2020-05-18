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

				menu = XLShredLib.ModMenu.Instance.gameObject.AddComponent<MultiplayerMenu>();
				utilityMenu = XLShredLib.ModMenu.Instance.gameObject.AddComponent<MultiplayerUtilityMenu>();

				uiBox = ModMenu.Instance.RegisterModMaker("Silentbaws", "Silentbaws", 0);
				uiBox.AddCustom("Patreon", DisplayPatreon, () => enabled);

				// TODO: Change this to a more stable approach
				Canvas[] canvasObjs = GameObject.FindObjectsOfType<Canvas>();
				foreach(Canvas obj in canvasObjs) {
					if (obj.name.Trim().Equals("Title Screen", StringComparison.CurrentCultureIgnoreCase)) {
						if (obj.transform.GetComponentsInChildren<Transform>().Length == 12) {
							GameObject.Destroy(obj);
						}
					}
				}


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
				patreonStyle.padding = new RectOffset(patreonStyle.padding.left, patreonStyle.padding.right, patreonStyle.padding.top, -20);
			}
			if (patreonButton == null) {
				patreonButton = new Texture2D(0, 0, TextureFormat.RGBA32, false);
				patreonButton.LoadImage(File.ReadAllBytes(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\patreon_button.png"));
				patreonButton.filterMode = FilterMode.Trilinear;
			}
			if (patreonOptions == null) {
				patreonOptions = new GUILayoutOption[] { GUILayout.Width(155), GUILayout.Height(36) };
			}

			GUILayout.Label("Reserve your username and <b><i><color=#f00>a</color><color=#ff7f00>d</color><color=#ff0>d</color> <color=#0f0>s</color><color=#0ff>o</color><color=#00f>m</color><color=#8b00ff>e</color> <color=#f00>f</color><color=#ff7f00>l</color><color=#ff0>a</color><color=#0f0>i</color><color=#0ff>r</color></i></b> by supporting me on patreon\n\n", patreonStyle);
			
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