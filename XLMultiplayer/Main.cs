using System;
using UnityEngine;
using UnityModManagerNet;
using Harmony12;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;
using XLShredLib.UI;
using XLShredLib;

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
				uiBox.AddLabel("Patreon", "Reserve your username and <b><i><color=#f00>a</color><color=#ff7f00>d</color><color=#ff0>d</color> <color=#0f0>s</color><color=#0ff>o</color><color=#00f>m</color><color=#8b00ff>e</color> <color=#f00>f</color><color=#ff7f00>l</color><color=#ff0>a</color><color=#0f0>i</color><color=#0ff>r</color></i></b> by supporting me on patreon\n\n", Side.left, () => enabled, 10);

				MultiplayerUtils.StartMapLoading();
			} else {
				//Unpatch the replay editor
				harmonyInstance.UnpatchAll(harmonyInstance.Id);

				MultiplayerUtils.StopMapLoading();

				multiplayerController.DisconnectFromServer();
				menu.CloseMultiplayerMenu();

				UnityEngine.Object.Destroy(menu);
				UnityEngine.Object.Destroy(utilityMenu);
			}

			return true;
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