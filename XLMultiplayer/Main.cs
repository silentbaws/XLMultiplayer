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
				harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

				menu = new GameObject().AddComponent<MultiplayerMenu>();
				statusMenu = new GameObject().AddComponent<MultiplayerStatusMenu>();
				UnityEngine.Object.DontDestroyOnLoad(menu.gameObject);
				UnityEngine.Object.DontDestroyOnLoad(statusMenu.gameObject);
				MultiplayerUtils.StartMapLoading();
			} else {
				harmonyInstance.UnpatchAll(harmonyInstance.Id);
				menu.EndMultiplayer();
				UnityEngine.Object.Destroy(menu.gameObject);
				UnityEngine.Object.Destroy(statusMenu.gameObject);
				MultiplayerUtils.StopMapLoading();
			}

			return true;
		}
	}

	//This works but remote client replay desyncs, look into that
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
					//BIG SHOUTOUT BLENDERMF
					CodeInstruction c = new CodeInstruction(OpCodes.Ldarg_0);
					yield return c;

					c = new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(ReplayEditor.ReplayPlaybackController), "ClipFrames").GetMethod);
					yield return c;

					c = new CodeInstruction(OpCodes.Ldc_I4_0);
					yield return c;

					c = new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(List<ReplayEditor.ReplayRecordedFrame>), "Item").GetMethod);
					yield return c;

					c = new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ReplayEditor.ReplayRecordedFrame), "time"));
					yield return c;
				} else {
					yield return instruction;
				}
			}
		}
	}
}
