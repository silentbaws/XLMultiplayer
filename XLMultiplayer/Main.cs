using System;
using UnityEngine;
using UnityModManagerNet;

namespace XLMultiplayer
{
	class Main {
		public static bool enabled;
		public static String modId;
		public static UnityModManager.ModEntry modEntry;

		public static MultiplayerMenu menu;

		static void Load(UnityModManager.ModEntry modEntry) {
			Main.modEntry = modEntry;
			Main.modId = modEntry.Info.Id;

			modEntry.OnToggle = OnToggle;
		}

		static bool OnToggle(UnityModManager.ModEntry modEntry, bool value) {
			if (value == enabled) return true;
			enabled = value;

			if (enabled) {
				menu = new GameObject().AddComponent<MultiplayerMenu>();
				UnityEngine.Object.DontDestroyOnLoad(menu.gameObject);
			} else {
				UnityEngine.Object.Destroy(menu);
			}

			return true;
		}
	}
}
