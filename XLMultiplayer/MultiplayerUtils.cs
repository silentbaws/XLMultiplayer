using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

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
		
		public static List<string> audioPlayerNames = new List<string>();

		private static Dictionary<string, byte> clipNameToArrayByteDict = new Dictionary<string, byte>();

		public static int hashedMaps = 0;

		public static string CalculateMD5Bytes(byte[] bytes) {
			using (var md5 = MD5.Create()) {
				byte[] hash = null;
				long size = bytes.Length;
				if (size > 10485760) {
					byte[] hashBytes = new byte[10485760];
					Array.Copy(bytes, 0, hashBytes, 0, 10485760);
					hash = md5.ComputeHash(hashBytes);
				} else {
					hash = md5.ComputeHash(bytes);
				}
				return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
			}
		}

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

		public static void ExtractZipContent(string FileZipPath, string OutputFolder) {
			ZipFile file = null;
			try {
				FileStream fs = File.OpenRead(FileZipPath);
				file = new ZipFile(fs);

				foreach (ZipEntry zipEntry in file) {
					if (!zipEntry.IsFile) {
						// Ignore directories
						continue;
					}

					String entryFileName = zipEntry.Name;
					// to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
					// Optionally match entrynames against a selection list here to skip as desired.
					// The unpacked length is available in the zipEntry.Size property.

					// 4K is optimum
					byte[] buffer = new byte[4096];
					Stream zipStream = file.GetInputStream(zipEntry);

					// Manipulate the output filename here as desired.
					String fullZipToPath = Path.Combine(OutputFolder, entryFileName);
					string directoryName = Path.GetDirectoryName(fullZipToPath);

					if (directoryName.Length > 0) {
						Directory.CreateDirectory(directoryName);
					}

					// Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
					// of the file, but does not waste memory.
					// The "using" will close the stream even if an exception occurs.
					using (FileStream streamWriter = File.Create(fullZipToPath)) {
						StreamUtils.Copy(zipStream, streamWriter, buffer);
					}
				}
			} finally {
				if (file != null) {
					file.IsStreamOwner = true; // Makes close also shut the underlying stream
					file.Close(); // Ensure we release resources
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
			mapsDictionary.Clear();

			if (!mapsDictionary.ContainsKey("0")) {
				mapsDictionary.Add("0", "Assets/_Scenes/Prototyping testing courthouse 2.unity");
			}
			if (!mapsDictionary.ContainsKey("1")) {
				mapsDictionary.Add("1", "Assets/_Scenes/Encinitas_scene.unity");
			}
			if (!mapsDictionary.ContainsKey("2")) {
				mapsDictionary.Add("2", "Assets/_Scenes/DowntownBlockout.unity");
			}
			if (!mapsDictionary.ContainsKey("3")) {
				mapsDictionary.Add("3", "Assets/_Scenes/CampEasyDay.unity");
			}
			if (!mapsDictionary.ContainsKey("4")) {
				mapsDictionary.Add("4", "Assets/_Scenes/SchoolBlockOut.unity");
			}
			if (!mapsDictionary.ContainsKey("5")) {
				mapsDictionary.Add("5", "Assets/_Scenes/HüdshitOfficial.unity");
			}
			if (!mapsDictionary.ContainsKey("6")) {
				mapsDictionary.Add("6", "Assets/_Scenes/STREETS Day.unity");
			}
			if (!mapsDictionary.ContainsKey("7")) {
				mapsDictionary.Add("7", "Assets/_Scenes/GrantSkateparkTest.unity");
			}
			
			List<string> files = LevelManager.Instance.CustomLevels.ConvertAll(levelInfo => (levelInfo.isAssetBundle ? levelInfo.path : null));
			int i = 0;

			hashedMaps = files.Count;
			loadingMaps = true;

			if (files == null || files.Count < 1) {
				UnityModManagerNet.UnityModManager.Logger.Log("**WARNING** XLMultiplayer COULD NOT find any maps in " + Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\SkaterXL\\Maps\\");
			}

			while (loadingMaps && files != null && files.Count > 0) {
				if (i < files.Count) {
					if (files[i] == null) {
						i++;
						continue;
					}

					string fileHash = CalculateMD5(files[i]);
					try {
						mapsDictionary.Add(fileHash, files[i]);
					} catch (ArgumentException) {
						mapsDictionary[fileHash] = files[i];
						duplicates++;
					}
					i++;
				}
				if (i >= files.Count) {
					loadedMaps = true;
					loadingMaps = false;
					hashingWatch.Stop();
					UnityModManagerNet.UnityModManager.Logger.Log($"[XLMultiplayer] Finished loading {files.Count} maps in {hashingWatch.ElapsedMilliseconds}ms with {duplicates} duplicates");
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

		public static void InitializeClipToArrayByteDict(Dictionary<string, AudioClip> originalClipDict) {
			clipNameToArrayByteDict.Clear();
			foreach (var KVP in originalClipDict) {
				try {
					byte arrByte = ArrayByteFromClipName(KVP.Key);

					UnityModManagerNet.UnityModManager.Logger.Log($"Added sound {KVP.Key} as {arrByte}");

					clipNameToArrayByteDict.Add(KVP.Key, arrByte);
				} catch (Exception e) {
					UnityModManagerNet.UnityModManager.Logger.Log($"Exception adding clip {KVP.Key} {e.ToString()}");
				}
			}
		}

		private static byte ArrayByteFromClipName(string sound) {
			if (DeckSounds.Instance.rollingSoundFast.name == sound) return 0;
			if (DeckSounds.Instance.rollingSoundSlow.name == sound) return 1;
			if (DeckSounds.Instance.tutorialBoardImpact.name == sound) return 2;
			if (DeckSounds.Instance.grassRollLoop.name == sound) return 3;
			if (ArrayContainsSoundByName(DeckSounds.Instance.bumps, sound)) return 4;
			if (ArrayContainsSoundByName(DeckSounds.Instance.ollieScooped, sound)) return 5;
			if (ArrayContainsSoundByName(DeckSounds.Instance.ollieSlow, sound)) return 6;
			if (ArrayContainsSoundByName(DeckSounds.Instance.ollieFast, sound)) return 7;
			if (ArrayContainsSoundByName(DeckSounds.Instance.boardLand, sound)) return 8;
			if (ArrayContainsSoundByName(DeckSounds.Instance.boardImpacts, sound)) return 9;
			if (ArrayContainsSoundByName(DeckSounds.Instance.bearingSounds, sound)) return 10;
			if (ArrayContainsSoundByName(DeckSounds.Instance.shoesBoardBackImpacts, sound)) return 11;
			if (ArrayContainsSoundByName(DeckSounds.Instance.shoesImpactGroundSole, sound)) return 12;
			if (ArrayContainsSoundByName(DeckSounds.Instance.shoesImpactGroundUpper, sound)) return 13;
			if (ArrayContainsSoundByName(DeckSounds.Instance.shoesMovementShort, sound)) return 14;
			if (ArrayContainsSoundByName(DeckSounds.Instance.shoesMovementLong, sound)) return 15;
			if (ArrayContainsSoundByName(DeckSounds.Instance.shoesPivotHeavy, sound)) return 16;
			if (ArrayContainsSoundByName(DeckSounds.Instance.shoesPivotLight, sound)) return 17;
			if (ArrayContainsSoundByName(DeckSounds.Instance.shoesPushImpact, sound)) return 18;
			if (ArrayContainsSoundByName(DeckSounds.Instance.shoesPushOff, sound)) return 19;
			if (ArrayContainsSoundByName(DeckSounds.Instance.concreteGrindGeneralStart, sound)) return 20;
			if (ArrayContainsSoundByName(DeckSounds.Instance.concreteGrindGeneralLoop, sound)) return 21;
			if (ArrayContainsSoundByName(DeckSounds.Instance.concreteGrindGeneralEnd, sound)) return 22;
			if (ArrayContainsSoundByName(DeckSounds.Instance.metalGrindGeneralStart, sound)) return 23;
			if (ArrayContainsSoundByName(DeckSounds.Instance.metalGrindGeneralLoop, sound)) return 24;
			if (ArrayContainsSoundByName(DeckSounds.Instance.metalGrindGeneralEnd, sound)) return 25;
			if (ArrayContainsSoundByName(DeckSounds.Instance.woodGrindGeneralStart, sound)) return 26;
			if (ArrayContainsSoundByName(DeckSounds.Instance.woodGrindGeneralLoop, sound)) return 27;
			if (ArrayContainsSoundByName(DeckSounds.Instance.woodGrindGeneralEnd, sound)) return 28;
			if (ArrayContainsSoundByName(DeckSounds.Instance.ollieWoodSlow, sound)) return 29;
			if (ArrayContainsSoundByName(DeckSounds.Instance.boardWoodLand, sound)) return 30;
			if (ArrayContainsSoundByName(DeckSounds.Instance.boardWoodImpacts, sound)) return 31;
			if (ArrayContainsSoundByName(DeckSounds.Instance.boardGrassImpacts, sound)) return 32;
			if (ArrayContainsSoundByName(DeckSounds.Instance.boardTarmacImpacts, sound)) return 33;
			if (ArrayContainsSoundByName(DeckSounds.Instance.concretePowerslideStart, sound)) return 34;
			if (ArrayContainsSoundByName(DeckSounds.Instance.concretePowerslideLoop, sound)) return 35;
			if (ArrayContainsSoundByName(DeckSounds.Instance.concretePowerslideEnd, sound)) return 36;
			if (ArrayContainsSoundByName(DeckSounds.Instance.tarmacPowerslideStart, sound)) return 37;
			if (ArrayContainsSoundByName(DeckSounds.Instance.tarmacPowerslideLoop, sound)) return 38;
			if (ArrayContainsSoundByName(DeckSounds.Instance.tarmacPowerslideEnd, sound)) return 39;
			if (ArrayContainsSoundByName(DeckSounds.Instance.brickPowerslideStart, sound)) return 40;
			if (ArrayContainsSoundByName(DeckSounds.Instance.brickPowerslideLoop, sound)) return 41;
			if (ArrayContainsSoundByName(DeckSounds.Instance.brickPowerslideEnd, sound)) return 42;
			if (ArrayContainsSoundByName(DeckSounds.Instance.woodPowerslideStart, sound)) return 43;
			if (ArrayContainsSoundByName(DeckSounds.Instance.woodPowerslideLoop, sound)) return 44;
			if (ArrayContainsSoundByName(DeckSounds.Instance.woodPowerslideEnd, sound)) return 45;
			if (ArrayContainsSoundByName(DeckSounds.Instance.movement_foley_jump, sound)) return 46;
			if (ArrayContainsSoundByName(DeckSounds.Instance.movement_foley_land, sound)) return 47;
			if (ArrayContainsSoundByName(DeckSounds.Instance.rollingBrickLoop, sound)) return 48;
			if (ArrayContainsSoundByName(DeckSounds.Instance.rollingTarmacLoop, sound)) return 49;
			if (ArrayContainsSoundByName(DeckSounds.Instance.rollingWoodLoop, sound)) return 50;
			if (ArrayContainsSoundByName(SoundManager.Instance.ragdollSounds.ragdoll_body, sound)) return 51;
			if (ArrayContainsSoundByName(SoundManager.Instance.ragdollSounds.ragdoll_legs_drag, sound)) return 52;
			if (ArrayContainsSoundByName(SoundManager.Instance.ragdollSounds.ragdoll_legs_hit, sound)) return 53;
			if (ArrayContainsSoundByName(SoundManager.Instance.ragdollSounds.ragdoll_metal, sound)) return 54;
			if (ArrayContainsSoundByName(SoundManager.Instance.ragdollSounds.ragdoll_arms, sound)) return 55;
			if (sound == null) return 254;
			return 255;
		}

		public static byte GetArrayByteFromClipName(string clipName) {
			if (clipName == null) return 254;
			byte retByte = 255;
			clipNameToArrayByteDict.TryGetValue(clipName, out retByte);
			return retByte;
		}

		public static string ClipNameFromArrayByte(byte arrayByte) {
			if (arrayByte == 254 || arrayByte == 255) return null;
			switch (arrayByte) {
				case 0:
					return DeckSounds.Instance.rollingSoundFast.name;
				case 1:
					return DeckSounds.Instance.rollingSoundSlow.name;
				case 2:
					return DeckSounds.Instance.tutorialBoardImpact.name;
				case 3:
					return DeckSounds.Instance.grassRollLoop.name;
				case 4:
					return DeckSounds.Instance.bumps[UnityEngine.Random.Range(0, DeckSounds.Instance.bumps.Length)].name;
				case 5:
					return DeckSounds.Instance.ollieScooped[UnityEngine.Random.Range(0, DeckSounds.Instance.ollieScooped.Length)].name;
				case 6:
					return DeckSounds.Instance.ollieSlow[UnityEngine.Random.Range(0, DeckSounds.Instance.ollieSlow.Length)].name;
				case 7:
					return DeckSounds.Instance.ollieFast[UnityEngine.Random.Range(0, DeckSounds.Instance.ollieFast.Length)].name;
				case 8:
					return DeckSounds.Instance.boardLand[UnityEngine.Random.Range(0, DeckSounds.Instance.boardLand.Length)].name;
				case 9:
					return DeckSounds.Instance.boardImpacts[UnityEngine.Random.Range(0, DeckSounds.Instance.boardImpacts.Length)].name;
				case 10:
					return DeckSounds.Instance.bearingSounds[UnityEngine.Random.Range(0, DeckSounds.Instance.bearingSounds.Length)].name;
				case 11:
					return DeckSounds.Instance.shoesBoardBackImpacts[UnityEngine.Random.Range(0, DeckSounds.Instance.shoesBoardBackImpacts.Length)].name;
				case 12:
					return DeckSounds.Instance.shoesImpactGroundSole[UnityEngine.Random.Range(0, DeckSounds.Instance.shoesImpactGroundSole.Length)].name;
				case 13:
					return DeckSounds.Instance.shoesImpactGroundUpper[UnityEngine.Random.Range(0, DeckSounds.Instance.shoesImpactGroundUpper.Length)].name;
				case 14:
					return DeckSounds.Instance.shoesMovementShort[UnityEngine.Random.Range(0, DeckSounds.Instance.shoesMovementShort.Length)].name;
				case 15:
					return DeckSounds.Instance.shoesMovementLong[UnityEngine.Random.Range(0, DeckSounds.Instance.shoesMovementLong.Length)].name;
				case 16:
					return DeckSounds.Instance.shoesPivotHeavy[UnityEngine.Random.Range(0, DeckSounds.Instance.shoesPivotHeavy.Length)].name;
				case 17:
					return DeckSounds.Instance.shoesPivotLight[UnityEngine.Random.Range(0, DeckSounds.Instance.shoesPivotLight.Length)].name;
				case 18:
					return DeckSounds.Instance.shoesPushImpact[UnityEngine.Random.Range(0, DeckSounds.Instance.shoesPushImpact.Length)].name;
				case 19:
					return DeckSounds.Instance.shoesPushOff[UnityEngine.Random.Range(0, DeckSounds.Instance.shoesPushOff.Length)].name;
				case 20:
					return DeckSounds.Instance.concreteGrindGeneralStart[UnityEngine.Random.Range(0, DeckSounds.Instance.concreteGrindGeneralStart.Length)].name;
				case 21:
					return DeckSounds.Instance.concreteGrindGeneralLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.concreteGrindGeneralLoop.Length)].name;
				case 22:
					return DeckSounds.Instance.concreteGrindGeneralEnd[UnityEngine.Random.Range(0, DeckSounds.Instance.concreteGrindGeneralEnd.Length)].name;
				case 23:
					return DeckSounds.Instance.metalGrindGeneralStart[UnityEngine.Random.Range(0, DeckSounds.Instance.metalGrindGeneralStart.Length)].name;
				case 24:
					return DeckSounds.Instance.metalGrindGeneralLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.metalGrindGeneralLoop.Length)].name;
				case 25:
					return DeckSounds.Instance.metalGrindGeneralEnd[UnityEngine.Random.Range(0, DeckSounds.Instance.metalGrindGeneralEnd.Length)].name;
				case 26:
					return DeckSounds.Instance.woodGrindGeneralStart[UnityEngine.Random.Range(0, DeckSounds.Instance.woodGrindGeneralStart.Length)].name;
				case 27:
					return DeckSounds.Instance.woodGrindGeneralLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.woodGrindGeneralLoop.Length)].name;
				case 28:
					return DeckSounds.Instance.woodGrindGeneralEnd[UnityEngine.Random.Range(0, DeckSounds.Instance.woodGrindGeneralEnd.Length)].name;
				case 29:
					return DeckSounds.Instance.ollieWoodSlow[UnityEngine.Random.Range(0, DeckSounds.Instance.ollieWoodSlow.Length)].name;
				case 30:
					return DeckSounds.Instance.boardWoodLand[UnityEngine.Random.Range(0, DeckSounds.Instance.boardWoodLand.Length)].name;
				case 31:
					return DeckSounds.Instance.boardWoodImpacts[UnityEngine.Random.Range(0, DeckSounds.Instance.boardWoodImpacts.Length)].name;
				case 32:
					return DeckSounds.Instance.boardGrassImpacts[UnityEngine.Random.Range(0, DeckSounds.Instance.boardGrassImpacts.Length)].name;
				case 33:
					return DeckSounds.Instance.boardTarmacImpacts[UnityEngine.Random.Range(0, DeckSounds.Instance.boardTarmacImpacts.Length)].name;
				case 34:
					return DeckSounds.Instance.concretePowerslideStart[UnityEngine.Random.Range(0, DeckSounds.Instance.concretePowerslideStart.Length)].name;
				case 35:
					return DeckSounds.Instance.concretePowerslideLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.concretePowerslideLoop.Length)].name;
				case 36:
					return DeckSounds.Instance.concretePowerslideEnd[UnityEngine.Random.Range(0, DeckSounds.Instance.concretePowerslideEnd.Length)].name;
				case 37:
					return DeckSounds.Instance.tarmacPowerslideStart[UnityEngine.Random.Range(0, DeckSounds.Instance.tarmacPowerslideStart.Length)].name;
				case 38:
					return DeckSounds.Instance.tarmacPowerslideLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.tarmacPowerslideLoop.Length)].name;
				case 39:
					return DeckSounds.Instance.tarmacPowerslideEnd[UnityEngine.Random.Range(0, DeckSounds.Instance.tarmacPowerslideEnd.Length)].name;
				case 40:
					return DeckSounds.Instance.brickPowerslideStart[UnityEngine.Random.Range(0, DeckSounds.Instance.brickPowerslideStart.Length)].name;
				case 41:
					return DeckSounds.Instance.brickPowerslideLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.brickPowerslideLoop.Length)].name;
				case 42:
					return DeckSounds.Instance.brickPowerslideEnd[UnityEngine.Random.Range(0, DeckSounds.Instance.brickPowerslideEnd.Length)].name;
				case 43:
					return DeckSounds.Instance.woodPowerslideStart[UnityEngine.Random.Range(0, DeckSounds.Instance.woodPowerslideStart.Length)].name;
				case 44:
					return DeckSounds.Instance.woodPowerslideLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.woodPowerslideLoop.Length)].name;
				case 45:
					return DeckSounds.Instance.woodPowerslideEnd[UnityEngine.Random.Range(0, DeckSounds.Instance.woodPowerslideEnd.Length)].name;
				case 46:
					return DeckSounds.Instance.movement_foley_jump[UnityEngine.Random.Range(0, DeckSounds.Instance.movement_foley_jump.Length)].name;
				case 47:
					return DeckSounds.Instance.movement_foley_land[UnityEngine.Random.Range(0, DeckSounds.Instance.movement_foley_land.Length)].name;
				case 48:
					return DeckSounds.Instance.rollingBrickLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.rollingBrickLoop.Length)].name;
				case 49:
					return DeckSounds.Instance.rollingTarmacLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.rollingTarmacLoop.Length)].name;
				case 50:
					return DeckSounds.Instance.rollingWoodLoop[UnityEngine.Random.Range(0, DeckSounds.Instance.rollingWoodLoop.Length)].name;
				case 51:
					return SoundManager.Instance.ragdollSounds.ragdoll_body[UnityEngine.Random.Range(0, SoundManager.Instance.ragdollSounds.ragdoll_body.Length)].name;
				case 52:
					return SoundManager.Instance.ragdollSounds.ragdoll_legs_drag[UnityEngine.Random.Range(0, SoundManager.Instance.ragdollSounds.ragdoll_legs_drag.Length)].name;
				case 53:
					return SoundManager.Instance.ragdollSounds.ragdoll_legs_hit[UnityEngine.Random.Range(0, SoundManager.Instance.ragdollSounds.ragdoll_legs_hit.Length)].name;
				case 54:
					return SoundManager.Instance.ragdollSounds.ragdoll_metal[UnityEngine.Random.Range(0, SoundManager.Instance.ragdollSounds.ragdoll_metal.Length)].name;
				case 55:
					return SoundManager.Instance.ragdollSounds.ragdoll_arms[UnityEngine.Random.Range(0, SoundManager.Instance.ragdollSounds.ragdoll_arms.Length)].name;
				default:
					return null;
			}
		}

		private static bool ArrayContainsSoundByName(AudioClip[] array, string sound) {
			foreach (var s in array) {
				if (s.name == sound) {
					return true;
				}
			}
			return false;
		}

		public static void RemoveAudioEventsOlderThanExcept<T>(List<T> events, float time, int amountToKeep) where T : ReplayEditor.AudioEvent {
			int amountToRemove = 0;
			while (events.Count - amountToRemove > amountToKeep && events[amountToKeep + amountToRemove].time < time) {
				amountToRemove++;
			}

			if (amountToRemove > 0) {
				events.RemoveRange(0, amountToRemove);
			}
		}
	}
}
