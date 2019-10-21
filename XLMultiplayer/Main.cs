using System;
using UnityEngine;
using UnityModManagerNet;
using Harmony12;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace XLMultiplayer
{
	class Main {
		public static bool enabled;
		public static String modId;
		public static UnityModManager.ModEntry modEntry;

		public static MultiplayerMenu menu;
		public static MultiplayerStatusMenu statusMenu;

		public static HarmonyInstance harmonyInstance;

		static void Load(UnityModManager.ModEntry modEntry) {
			Main.modEntry = modEntry;
			Main.modId = modEntry.Info.Id;

			modEntry.OnToggle = OnToggle;
		}

		static bool OnToggle(UnityModManager.ModEntry modEntry, bool value) {
			if (value == enabled) return true;
			enabled = value;

			if (enabled) {
				harmonyInstance = HarmonyInstance.Create(modEntry.Info.Id);
				//harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

				menu = new GameObject().AddComponent<MultiplayerMenu>();
				statusMenu = new GameObject().AddComponent<MultiplayerStatusMenu>();
				UnityEngine.Object.DontDestroyOnLoad(menu.gameObject);
				UnityEngine.Object.DontDestroyOnLoad(statusMenu.gameObject);
				MultiplayerUtils.StartMapLoading();
			} else {
				//harmonyInstance.UnpatchAll(harmonyInstance.Id);
				menu.EndMultiplayer();
				UnityEngine.Object.Destroy(menu.gameObject);
				UnityEngine.Object.Destroy(statusMenu.gameObject);
				MultiplayerUtils.StopMapLoading();
			}

			return true;
		}
	}

	//This doesn't work for some reason, well it works for the local client but remote clients are just balls? wtf... Idk just keep it clamped to 0 for now I guess
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
				if(instruction.opcode == OpCodes.Ldc_R4) {
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ReplaySetPlaybackTimeExentions), nameof(ReplaySetPlaybackTimeExentions.GetFrameZeroTime)));
				} else {
					UnityModManager.Logger.Log(instruction.ToString());
					yield return instruction;
				}
			}
		}
	}

	static class ReplaySetPlaybackTimeExentions {
		public static float GetFrameZeroTime(this ReplayEditor.ReplayPlaybackController controller) {
			return controller.ClipFrames[0].time;
		}
	}
}
