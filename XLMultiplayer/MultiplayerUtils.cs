using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace XLMultiplayer {
	class MultiplayerUtils {
		public static Dictionary<string, string> mapsDictionary = new Dictionary<string, string>();
		public static Dictionary<string, string> serverMapDictionary = new Dictionary<string, string>();
		public static bool loadedMaps = false;

		private static bool loadingMaps = false;
		private static Thread loadingThread;

		public static string currentVote = "current";

		private static Stopwatch hashingWatch;
		private static int duplicates = 0;

		private static string CalculateMD5(string filename) {
			using (var md5 = MD5.Create()) {
				using (var stream = File.OpenRead(filename)) {
					byte[] hash = null;
					long size = new System.IO.FileInfo(filename).Length;
					if (size > 10485760) {
						byte[] bytes = new byte[10485760];
						stream.Read(bytes, 0, 10485760);
						hash = md5.ComputeHash(bytes);
					} else {
						hash = md5.ComputeHash(stream);
					}
					return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
				}
			}
		}

		public static void LoadServerMaps(byte[] mapListBytes) {
			int readBytes = 1;

			serverMapDictionary.Clear();

			if (!serverMapDictionary.ContainsKey("current")) {
				serverMapDictionary.Add("current", "current");
			}
			currentVote = "current";

			while(readBytes < mapListBytes.Length) {
				int mapHashLength = BitConverter.ToInt32(mapListBytes, readBytes);
				readBytes += 4;

				string mapHash = ASCIIEncoding.ASCII.GetString(mapListBytes, readBytes, mapHashLength);
				readBytes += mapHashLength;

				int mapNameLength = BitConverter.ToInt32(mapListBytes, readBytes);
				readBytes += 4;

				string mapName = ASCIIEncoding.ASCII.GetString(mapListBytes, readBytes, mapNameLength);
				readBytes += mapNameLength;

				try {
					serverMapDictionary.Add(mapHash, mapName);
				} catch (ArgumentException) {
					serverMapDictionary[mapHash] = mapName;
				}
			}
		}

		private static void LoadMapHashes() {
			string mapsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SkaterXL\\Maps\\";
			string[] files = Directory.GetFiles(mapsFolder);

			int i = 0;
			loadingMaps = true;

			if (!mapsDictionary.ContainsKey("0")) {
				mapsDictionary.Add("0", "Assets/_Scenes/Prototyping testing courthouse 2.unity");
			}
			if (!mapsDictionary.ContainsKey("1")) {
				mapsDictionary.Add("1", "Assets/_Scenes/Encinitas_scene.unity");
			}

			while (loadingMaps) {
				string fileHash = CalculateMD5(files[i]);
				try {
					mapsDictionary.Add(fileHash, files[i]);
				} catch (ArgumentException) {
					mapsDictionary[fileHash] = files[i];
					duplicates++;
				}
				i++;
				if (i == files.Length) {
					loadedMaps = true;
					loadingMaps = false;
					hashingWatch.Stop();
					UnityModManagerNet.UnityModManager.Logger.Log($"[XLMultiplayer] Finished loading {files.Length} maps in {hashingWatch.ElapsedMilliseconds}ms with {duplicates} duplicates");
					break;
				}
			}
		}

		public static void StartMapLoading() {
			if(loadingThread == null && !loadedMaps) {
				UnityModManagerNet.UnityModManager.Logger.Log($"[XLMultiplayer] Starting thread to hash all maps");
				loadingThread = new Thread(LoadMapHashes);
				loadingThread.IsBackground = true;

				hashingWatch = Stopwatch.StartNew();

				loadingThread.Start();
			}
		}

		public static void StopMapLoading() {
			if(loadingThread != null) {
				loadingMaps = false;
				loadingThread.Join();
				loadingThread = null;
			}
		}

		public static string ChangeMap(byte[] mapHash) {
			int nameLength = BitConverter.ToInt32(mapHash, 1);
			string mapName = ASCIIEncoding.ASCII.GetString(mapHash, 5, nameLength);

			int hashLength = BitConverter.ToInt32(mapHash, 5 + nameLength);
			string hash = ASCIIEncoding.ASCII.GetString(mapHash, 9 + nameLength, hashLength);

			string mapPath = "";
			if(mapsDictionary.TryGetValue(hash, out mapPath)) {
				currentVote = "current";
				Main.menu.multiplayerManager.StartLoadMap(mapPath);
				return "";
			}
			return mapName;
		}
	}
}
