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

		public static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV) {
			// Check arguments.
			if (plainText == null || plainText.Length <= 0)
				throw new ArgumentNullException("plainText");
			if (Key == null || Key.Length <= 0)
				throw new ArgumentNullException("Key");
			if (IV == null || IV.Length <= 0)
				throw new ArgumentNullException("IV");

			byte[] data = ASCIIEncoding.ASCII.GetBytes(plainText);

			// Create an Aes object
			// with the specified key and IV.
			using (Aes aesAlg = Aes.Create()) {
				aesAlg.Mode = CipherMode.CBC;
				aesAlg.KeySize = 256;
				aesAlg.BlockSize = 128;
				aesAlg.Key = Key;
				aesAlg.IV = IV;

				// Create an encryptor to perform the stream transform.
				ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

				using (var ms = new MemoryStream())
				using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write)) {
					cryptoStream.Write(data, 0, data.Length);
					cryptoStream.FlushFinalBlock();

					//byte[] returnValue = new byte[data.Length];
					//Array.Copy(ms.ToArray(), 0, returnValue, 0, returnValue.Length);

					return ms.ToArray();
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

			while (readBytes < mapListBytes.Length) {
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
			if (!mapsDictionary.ContainsKey("0")) {
				mapsDictionary.Add("0", "Assets/_Scenes/Prototyping testing courthouse 2.unity");
			}
			if (!mapsDictionary.ContainsKey("1")) {
				mapsDictionary.Add("1", "Assets/_Scenes/Encinitas_scene.unity");
			}

			string mapsFolder = "";
			string[] files = null;
			int i = 0;

			try {
				mapsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SkaterXL\\Maps\\";
				files = Directory.GetFiles(mapsFolder);

				loadingMaps = true;
			} catch (Exception) {
				UnityModManagerNet.UnityModManager.Logger.Log("Failed to find maps folder or retrieve files from folder");
			}

			if (files == null || files.Length < 1) {
				UnityModManagerNet.UnityModManager.Logger.Log("**WARNING** XLMultiplayer COULD NOT find any maps in " + Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SkaterXL\\Maps\\");
			}

			while (loadingMaps && files != null && files.Length > 0) {
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
			if (loadingThread == null && !loadedMaps) {
				UnityModManagerNet.UnityModManager.Logger.Log($"[XLMultiplayer] Starting thread to hash all maps");
				loadingThread = new Thread(LoadMapHashes);
				loadingThread.IsBackground = true;

				hashingWatch = Stopwatch.StartNew();

				loadingThread.Start();
			}
		}

		public static void StopMapLoading() {
			if (loadingThread != null) {
				loadingMaps = false;
				loadingThread.Join();
				loadingThread = null;
			}
		}

		public static string ChangeMap(byte[] mapHash) {
			int nameLength = BitConverter.ToInt32(mapHash, 1);
			string mapName = ASCIIEncoding.ASCII.GetString(mapHash, 5, nameLength);
			
			string hash = ASCIIEncoding.ASCII.GetString(mapHash, 5 + nameLength, mapHash.Length - nameLength - 5);

			UnityModManagerNet.UnityModManager.Logger.Log($"[XLMultiplayer] Trying to switch to map with hash {hash}");

			string mapPath = "";
			if (mapsDictionary.TryGetValue(hash, out mapPath)) {
				currentVote = "current";
				Main.multiplayerController.StartLoadMap(mapPath);
				return null;
			}
			return mapName;
		}
	}
}
