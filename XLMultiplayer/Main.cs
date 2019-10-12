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
				statusMenu = new GameObject().AddComponent<MultiplayerStatusMenu>();
				UnityEngine.Object.DontDestroyOnLoad(menu.gameObject);
				UnityEngine.Object.DontDestroyOnLoad(statusMenu.gameObject);
				MultiplayerUtils.StartMapLoading();
			} else {
				menu.EndMultiplayer();
				UnityEngine.Object.Destroy(menu.gameObject);
				UnityEngine.Object.Destroy(statusMenu.gameObject);
				MultiplayerUtils.StopMapLoading();
			}

			return true;
		}
	}
}
