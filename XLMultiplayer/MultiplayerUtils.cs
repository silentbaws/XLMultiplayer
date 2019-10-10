using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace XLMultiplayer {
	class MultiplayerUtils {
		static public Dictionary<string, string> mapsDictionary = new Dictionary<string, string>();

		static string CalculateMD5(string filename) {
			using (var md5 = MD5.Create()) {
				using (var stream = File.OpenRead(filename)) {
					var hash = md5.ComputeHash(stream);
					return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
				}
			}
		}

		static void LoadMapHashes() {
			string mapsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/SkaterXL/Maps/";

			foreach(string file in Directory.GetFiles(mapsFolder)) {
				string fileHash = CalculateMD5(file);
				mapsDictionary.Add(fileHash, file);
			}
		}
	}
}
