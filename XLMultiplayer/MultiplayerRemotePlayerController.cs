using HarmonyLib;
using ReplayEditor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace XLMultiplayer {
	public class MultiplayerFrameBufferObject {
		public Vector3[] vectors = null;
		public Quaternion[] quaternions = null;

		public MultiplayerFrameBufferObject replayFrameBufferObject;

		public int animFrame = 0;

		public bool key = false;

		public float realFrameTime = -1f;
		public float frameTime;
		public float deltaTime = 0;
		public float timeSinceStart = 0;

		public MultiplayerFrameBufferObject() {
			animFrame = 0;
			deltaTime = 0;
			timeSinceStart = 0;

			replayFrameBufferObject = this;
		}
	}

	public class MultiplayerSoundBufferObject {
		public List<AudioOneShotEvent>[] audioOneShots;
		public List<AudioClipEvent>[] audioClipEvents;
		public List<AudioVolumeEvent>[] audioVolumeEvents;
		public List<AudioPitchEvent>[] audioPitchEvents;
		public List<AudioCutoffEvent>[] audioCutoffEvents;

		public float playTime = 0f;
		public bool setRealTime = false;

		public MultiplayerSoundBufferObject(int arraySize) {
			audioOneShots = new List<AudioOneShotEvent>[arraySize];
			audioClipEvents = new List<AudioClipEvent>[arraySize];
			audioVolumeEvents = new List<AudioVolumeEvent>[arraySize];
			audioPitchEvents = new List<AudioPitchEvent>[arraySize];
			audioCutoffEvents = new List<AudioCutoffEvent>[arraySize];
		}

		public void AdjustRealTimeToAnimation(MultiplayerFrameBufferObject animationFrame) {
			int audioPlayerCount = MultiplayerUtils.audioPlayerNames.Count;

			for (int i = 0; i < audioPlayerCount; i++) {
				int oneShotsCount = audioOneShots[i].Count;
				for (int j = 0; j < oneShotsCount; j++) {
					audioOneShots[i][j].time = animationFrame.realFrameTime + animationFrame.frameTime - audioOneShots[i][j].time;
				}

				int clipEventsCount = audioClipEvents[i].Count;
				for (int j = 0; j < clipEventsCount; j++) {
					audioClipEvents[i][j].time = animationFrame.realFrameTime + animationFrame.frameTime - audioClipEvents[i][j].time;
				}

				int volumeEventsCount = audioVolumeEvents[i].Count;
				for (int j = 0; j < volumeEventsCount; j++) {
					audioVolumeEvents[i][j].time = animationFrame.realFrameTime + animationFrame.frameTime - audioVolumeEvents[i][j].time;
				}

				int pitchEventsCount = audioPitchEvents[i].Count;
				for (int j = 0; j < pitchEventsCount; j++) {
					audioPitchEvents[i][j].time = animationFrame.realFrameTime + animationFrame.frameTime - audioPitchEvents[i][j].time;
				}

				int cutoffEventsCount = audioCutoffEvents[i].Count;
				for (int j = 0; j < cutoffEventsCount; j++) {
					audioCutoffEvents[i][j].time = animationFrame.realFrameTime + animationFrame.frameTime - audioCutoffEvents[i][j].time;
				}
			}

			setRealTime = true;
		}
		
		public void AddSoundsToPlayers(ReplayPlaybackController replayController, Dictionary<string, ReplayAudioEventPlayer> replayEventPlayerForName) {
			int audioPlayersCount = MultiplayerUtils.audioPlayerNames.Count;
			for (int i = 0; i < audioPlayersCount; i++) {
				List<AudioOneShotEvent> currentOneShotEventList = audioOneShots[i].Count > 0 ? audioOneShots[i] : null;
				List<AudioClipEvent> currentClipEventList = audioClipEvents[i].Count > 0 ? audioClipEvents[i] : null;
				List<AudioVolumeEvent> currentVolumeEventList = audioVolumeEvents[i].Count > 0 ? audioVolumeEvents[i] : null;
				List<AudioPitchEvent> currentPitchEventList = audioPitchEvents[i].Count > 0 ? audioPitchEvents[i] : null;
				List<AudioCutoffEvent> currentCutoffEventList = audioCutoffEvents[i].Count > 0 ? audioCutoffEvents[i] : null;

				if (currentOneShotEventList != null && replayController.AudioEventPlayers[i].oneShotEvents == null) {
					replayController.AudioEventPlayers[i].LoadOneShotEvents(currentOneShotEventList);
				} else if (currentOneShotEventList != null) replayController.AudioEventPlayers[i].oneShotEvents.AddRange(currentOneShotEventList);

				if (currentClipEventList != null && replayController.AudioEventPlayers[i].clipEvents == null) {
					replayController.AudioEventPlayers[i].LoadClipEvents(currentClipEventList);
				} else if (currentClipEventList != null) replayController.AudioEventPlayers[i].clipEvents.AddRange(currentClipEventList);

				if (currentVolumeEventList != null && replayController.AudioEventPlayers[i].volumeEvents == null) {
					replayController.AudioEventPlayers[i].LoadVolumeEvents(currentVolumeEventList);
				} else if (currentVolumeEventList != null ) replayController.AudioEventPlayers[i].volumeEvents.AddRange(currentVolumeEventList);

				if (currentPitchEventList != null && replayController.AudioEventPlayers[i].pitchEvents == null) {
					replayController.AudioEventPlayers[i].LoadPitchEvents(currentPitchEventList);
				} else if (currentPitchEventList != null) replayController.AudioEventPlayers[i].pitchEvents.AddRange(currentPitchEventList);

				if (currentCutoffEventList != null && replayController.AudioEventPlayers[i].cutoffEvents == null) {
					replayController.AudioEventPlayers[i].LoadCutoffEvents(currentCutoffEventList);
				} else if (currentCutoffEventList != null) replayController.AudioEventPlayers[i].cutoffEvents.AddRange(currentCutoffEventList);
			}
		}
	}

	public class MultiplayerRemotePlayerController : MultiplayerPlayerController {
		public GameObject skater { get; private set; }
		public GameObject board { get; private set; }

		public GameObject usernameObject;
		private TextMesh usernameText;
		private string currentlySetUsername = "";

		private int currentAnimationPacket = -1;
		private Transform[] bones;

		private bool startedAnimating = false;
		private bool loadedTextures = false;
		private bool receivedTextures = false;
		private float previousFrameTime = 0;
		private float firstFrameTime = -1f;
		private float startAnimTime = -1f;
		public float lastReplayFrame = 1f;

		private bool waitingForDelay = false;
		private bool speedDelay = false;

		private List<byte> gearStream = new List<byte>();

		private Transform mainCameraTransform;

		public List<MultiplayerFrameBufferObject> animationFrames = new List<MultiplayerFrameBufferObject>();
		public List<MultiplayerFrameBufferObject> replayAnimationFrames = new List<MultiplayerFrameBufferObject>();
		public List<ReplayRecordedFrame> recordedFrames = new List<ReplayRecordedFrame>();

		public List<MultiplayerSoundBufferObject> soundQueue = new List<MultiplayerSoundBufferObject>();

		public List<MultiplayerRemoteTexture> multiplayerTextures = new List<MultiplayerRemoteTexture>();
		public string bodyType = "male";

		public ReplayPlaybackController replayController;

		public byte playerID = 255;

		public MultiplayerRemotePlayerController(StreamWriter writer) : base(writer) {  }

		Dictionary<string, ReplayAudioEventPlayer> replayEventPlayerForName = null;

		override public void ConstructPlayer() {
			//Create a new root object for the player
			this.player = GameObject.Instantiate<GameObject>(ReplayEditorController.Instance.playbackController.gameObject);

			UnityEngine.Object.DontDestroyOnLoad(this.player);
			this.player.name = "New Player";
			this.player.transform.SetParent(null);
			this.player.transform.position = PlayerController.Instance.transform.position;
			this.player.transform.rotation = PlayerController.Instance.transform.rotation;
			debugWriter.WriteLine("Created New Player");

			this.replayController = this.player.GetComponentInChildren<ReplayPlaybackController>();

			this.bones = replayController.playbackTransforms.ToArray();

			this.player.SetActive(true);

			this.board = this.player.transform.Find("Skateboard").gameObject;
			this.board.transform.position = new Vector3(0, 0, 0);
			this.board.name = "New Player Board";

			this.skater = this.player.transform.Find("NewSkater").gameObject;
			this.skater.transform.position = new Vector3(0, 0, 0);
			this.skater.name = "New Player Skater";

			debugWriter.WriteLine("created everything");

			Traverse.Create(characterCustomizer).Field("_bonesDict").SetValue(bones.ToDictionary((Transform t) => t.name));
			characterCustomizer.enabled = true;

			characterCustomizer.LoadLastPlayer();

			this.usernameObject = new GameObject("Username Object");
			this.usernameObject.transform.SetParent(this.player.transform, false);
			this.usernameObject.transform.localScale = new Vector3(-0.01f, 0.01f, 1f);
			this.usernameObject.AddComponent<MeshRenderer>();
			this.usernameObject.GetComponent<MeshRenderer>().material = Resources.FindObjectsOfTypeAll<Font>()[0].material;
			this.usernameText = this.usernameObject.AddComponent<TextMesh>();
			this.usernameText.text = username;
			this.usernameText.fontSize = 256;
			this.usernameText.font = Resources.FindObjectsOfTypeAll<Font>()[0];
			this.usernameText.color = Color.black;
			this.usernameText.alignment = TextAlignment.Center;

			mainCameraTransform = Camera.main.transform;
			
			if (replayEventPlayerForName == null) {
				replayEventPlayerForName = new Dictionary<string, ReplayAudioEventPlayer>();
				for (int i = 0; i < MultiplayerUtils.audioPlayerNames.Count; i++) {
					foreach (ReplayAudioEventPlayer audioPlayer in replayController.AudioEventPlayers) {
						if (audioPlayer.name.Equals(MultiplayerUtils.audioPlayerNames[i])) {
							replayEventPlayerForName.Add(MultiplayerUtils.audioPlayerNames[i], audioPlayer);
							break;
						}
					}
				}
			}
		}

		public void ParseTextureStream(byte[] inTextureStream) {
			ushort currentMessage = BitConverter.ToUInt16(inTextureStream, 0);
			ushort totalMessages = BitConverter.ToUInt16(inTextureStream, 2);

			for (int i = 4; i < inTextureStream.Length; i++) {
				gearStream.Add(inTextureStream[i]);
			}

			this.debugWriter.WriteLine($"Texture {currentMessage}/{totalMessages}");

			if (currentMessage != totalMessages) {
				return;
			}
			this.debugWriter.WriteLine($"Finished Texture loading");

			byte[] textureStream = gearStream.ToArray();

			ushort bodyTypeLen = BitConverter.ToUInt16(textureStream, 0);
			this.bodyType = Encoding.UTF8.GetString(textureStream, 2, bodyTypeLen);

			UnityModManagerNet.UnityModManager.Logger.Log($"Attempting to equip body with name length {bodyTypeLen} and name: {bodyType}!");

			int readBytes = 2 + bodyTypeLen;

			GearInfo[][][] allInfos = Traverse.Create(GearDatabase.Instance).Field("gearListSource").GetValue<GearInfo[][][]>();

			receivedBody = GetGearInfoFromPath(bodyType, allInfos, true);


			while (readBytes < textureStream.Length - 1) {
				UnityModManagerNet.UnityModManager.Logger.Log($"Read {readBytes} bytes of {textureStream.Length}");
				bool customTex = textureStream[readBytes] == 1 ? true : false;
				GearInfoType texInfotype = (GearInfoType)textureStream[readBytes + 1];

				readBytes += 2;
				ushort typeLen = BitConverter.ToUInt16(textureStream, readBytes);
				int dataLen = BitConverter.ToInt32(textureStream, readBytes + 2);

				string texType = Encoding.UTF8.GetString(textureStream, readBytes + 6, typeLen);
				readBytes += 6 + typeLen;
				string gearPath = "";
				byte[] texData = null;
				MultiplayerRemoteTexture newTexture = null;
				if (customTex) {
					texData = new byte[dataLen];
					Array.Copy(textureStream, readBytes, texData, 0, dataLen);
					readBytes += dataLen;

					newTexture = new MultiplayerRemoteTexture(customTex, gearPath, texType, texInfotype, this.debugWriter);
				} else {
					gearPath = Encoding.UTF8.GetString(textureStream, readBytes, dataLen);
					readBytes += dataLen;

					newTexture = new MultiplayerRemoteTexture(GetGearInfoFromPath(gearPath, allInfos), customTex, this.debugWriter);
					newTexture.textureType = texType;
					newTexture.infoType = texInfotype;
				}

				multiplayerTextures.Add(newTexture);
				newTexture.SaveTexture(this.playerID, texData);
			}

			receivedTextures = true;
		}

		GearInfo receivedBody = null;

		public GearInfo GetGearInfoFromPath(string path, GearInfo[][][] allInfos, bool body = false) {
			for (int i = 0; i < allInfos.Length; i++) {
				for (int j = 0; j < allInfos[i].Length; j++) {
					foreach (GearInfo info in allInfos[i][j]) {
						foreach (MaterialChange change in info.GetMaterialChanges()) {
							foreach (TextureChange texChange in change.textureChanges) {
								if (texChange.texturePath.Equals(path, StringComparison.CurrentCultureIgnoreCase)) {
									return info;
								}
							}
						}

						if (body) {
							CharacterBodyInfo currentComp = info as CharacterBodyInfo;
							if (currentComp != null && path.Equals(currentComp.ToString(), StringComparison.CurrentCultureIgnoreCase)) {
								UnityModManagerNet.UnityModManager.Logger.Log($"Found matching body {path}");
								return info;
							}
						}
					}
				}
			}
			return null;
		}

		public void ApplyTextures() {
			if (this.receivedTextures && !this.loadedTextures && GameManagement.GameStateMachine.Instance.CurrentState.GetType() != typeof(GameManagement.ReplayState)) {
				characterCustomizer.RemoveAllGear();
				characterCustomizer.RemoveAllGear(true);

				Transform[] ignoredObjects = characterCustomizer.WheelParents.Union(characterCustomizer.TruckHangerParents).Union(characterCustomizer.TruckBaseParents).Append(characterCustomizer.DeckParent).Append(characterCustomizer.ClothingParent).ToArray();
				foreach (Transform t in ignoredObjects) {
					Transform[] childrenTransforms = t.GetComponentsInChildren<Transform>();
					if (childrenTransforms == null || childrenTransforms.Length == 0) {
						continue;
					}
					foreach (Transform t2 in childrenTransforms) {
						if (t2 == null || t2.gameObject == null) continue;
						if (t2.gameObject.layer == LayerMask.NameToLayer("Character") || t2.gameObject.layer == LayerMask.NameToLayer("Skateboard")) {
							if (!ignoredObjects.Contains(t2)) {
								GameObject.Destroy(t2.gameObject);
							}
						}
					}
				}

				if (receivedBody != null) {
					characterCustomizer.EquipGear(receivedBody);
				} else {
					UnityModManagerNet.UnityModManager.Logger.Log($"Attempting to find skater with body ID {bodyType.Replace("Body ", "").Split('_')[0]}");
					SkaterInfo skaterInfo = GearDatabase.Instance.skaters.FirstOrDefault((SkaterInfo s) => s.bodyID.ToLower().Equals(bodyType.Replace("Body ", "").Split('_')[0], StringComparison.CurrentCultureIgnoreCase));
					if (skaterInfo != null) {
						UnityModManager.Logger.Log("Found it");
						characterCustomizer.LoadCustomizations(skaterInfo.customizations);
					}
				}

				foreach (MultiplayerRemoteTexture mpTex in multiplayerTextures) {
					if (mpTex.saved && !mpTex.loaded && !(mpTex.infoType == GearInfoType.Body)) {
						mpTex.LoadFromFileMainThread(this);
					}  else if (mpTex.saved && !mpTex.loaded && mpTex.textureType.Equals("head", StringComparison.InvariantCultureIgnoreCase)) {
					MultiplayerRemoteTexture bodyTex = multiplayerTextures.Find((t) => t.textureType.Equals("body", StringComparison.InvariantCultureIgnoreCase));
					if (bodyTex != null || !mpTex.isCustom) {
						UnityModManagerNet.UnityModManager.Logger.Log($"Updating body textures for {this.bodyType} id: {this.playerID}");

						bodyTex.loaded = true;
						mpTex.loaded = true;

						if (mpTex.isCustom) {
							TextureChange[] headChange = { new TextureChange("albedo", mpTex.path) };
							TextureChange[] bodyChange = { new TextureChange("albedo", bodyTex.path) };

							List<MaterialChange> materialChanges = new List<MaterialChange>();
							materialChanges.Add(new MaterialChange("body", bodyChange));
							materialChanges.Add(new MaterialChange("head", headChange));

							CharacterBodyInfo bodyInfo = new CharacterBodyInfo("MP Temp body", this.bodyType, mpTex.isCustom, materialChanges, new string[0]);

							characterCustomizer.EquipGear(bodyInfo);
						} else {
							characterCustomizer.EquipGear(mpTex.info as CharacterBodyInfo);
						}
					}
				}
			}

				loadedTextures = true;
			}
		}

		public void SetPlayerTexture(string path, string texType, GearInfoType infoType, bool custom, GearInfo info = null) {
			GearInfo newInfo = null;
			TextureChange texChange = null;
			if (custom) {
				texChange = new TextureChange("albedo", path);
			}

			if (infoType == GearInfoType.Board) {
				if (custom) {
					newInfo = new BoardGearInfo("MP Temp " + texType.ToString(), texType, custom, new TextureChange[] { texChange }, new string[0]);
				} else {
					newInfo = info as BoardGearInfo;
				}

			} else {
				if (custom) {
					newInfo = new CharacterGearInfo("MP Temp " + texType.ToString(), texType, custom, new TextureChange[] { texChange }, new string[0]);
				} else {
					newInfo = info as CharacterGearInfo;
				}
			}
			
			
			characterCustomizer.EquipGear(newInfo);
		}

		public void UnpackAnimations(byte[] recBuffer) {
			int receivedPacketSequence = BitConverter.ToInt32(recBuffer, 0);
			
			bool ordered = true;
			if (receivedPacketSequence < currentAnimationPacket - 5) {
				ordered = false;
			} else {
				currentAnimationPacket = Math.Max(currentAnimationPacket, receivedPacketSequence);
			}

			MultiplayerFrameBufferObject currentBufferObject = new MultiplayerFrameBufferObject();

			currentBufferObject.key = recBuffer[4] == (byte)1 ? true : false;
			currentBufferObject.animFrame = receivedPacketSequence;

			currentBufferObject.frameTime = BitConverter.ToSingle(recBuffer, recBuffer.Length - 4);

			if (this.firstFrameTime != -1f && currentBufferObject.frameTime < this.firstFrameTime) {
				return;
			}

			Vector3[] vectors = new Vector3[77];
			Quaternion[] quaternions = new Quaternion[77];

			float[] floatValues = null;
			ushort[] halfValues = null;
			float[] precisePosition = null;
			SystemHalf.Half[] halfArray = null;

			if (currentBufferObject.key) {
				halfValues = new ushort[77 * 6 + 12];
				halfArray = new SystemHalf.Half[77 * 6 + 12];

				Buffer.BlockCopy(recBuffer, 5, halfValues, 0, halfValues.Length * sizeof(ushort));
				precisePosition = new float[12];
				Buffer.BlockCopy(recBuffer, 5, precisePosition, 0, 24);
				Buffer.BlockCopy(recBuffer, 5 + 96, precisePosition, 24, 24);

				for (int i = 0; i < halfValues.Length; i++) {
					halfArray[i] = new SystemHalf.Half();
					halfArray[i].Value = halfValues[i];
				}
			} else {
				floatValues = new float[77 * 6];

				Buffer.BlockCopy(recBuffer, 5, floatValues, 0, 77 * 6 * sizeof(float));
			}
			
			for (int i = 0; i < 77; i++) {
				if (currentBufferObject.key) {
					int offset = i > 7 ? 6 : 0;
					vectors[i].x = SystemHalf.HalfHelper.HalfToSingle(halfArray[i * 6 + 6 + offset]);
					vectors[i].y = SystemHalf.HalfHelper.HalfToSingle(halfArray[i * 6 + 7 + offset]);
					vectors[i].z = SystemHalf.HalfHelper.HalfToSingle(halfArray[i * 6 + 8 + offset]);

					quaternions[i].eulerAngles = new Vector3(SystemHalf.HalfHelper.HalfToSingle(halfArray[i * 6 + 9 + offset]),
																	SystemHalf.HalfHelper.HalfToSingle(halfArray[i * 6 + 10 + offset]),
																	SystemHalf.HalfHelper.HalfToSingle(halfArray[i * 6 + 11 + offset]));
				} else {
					vectors[i].x = floatValues[i * 6];
					vectors[i].y = floatValues[i * 6 + 1];
					vectors[i].z = floatValues[i * 6 + 2];

					quaternions[i].eulerAngles = new Vector3(floatValues[i * 6 + 3], floatValues[i * 6 + 4], floatValues[i * 6 + 5]);
				}
			}

			if (currentBufferObject.key) {
				vectors[0].x = precisePosition[0];
				vectors[0].y = precisePosition[1]; 
				vectors[0].z = precisePosition[2];

				quaternions[0].eulerAngles = new Vector3(precisePosition[3], precisePosition[4], precisePosition[5]);

				vectors[7].x = precisePosition[6];
				vectors[7].y = precisePosition[7];
				vectors[7].z = precisePosition[8];
				quaternions[7].eulerAngles = new Vector3(precisePosition[9], precisePosition[10], precisePosition[11]);
			}

			currentBufferObject.vectors = vectors;
			currentBufferObject.quaternions = quaternions;

			if (ordered) {
				this.animationFrames.Add(currentBufferObject);

				this.animationFrames.Sort((f, f2) => f.animFrame.CompareTo(f2.animFrame));
				//this.animationFrames = this.animationFrames.OrderBy(f => f.animFrame).ToList();
			}

			if ((this.replayAnimationFrames.Count > 0 && this.replayAnimationFrames[0] != null && this.replayAnimationFrames[0].animFrame > currentBufferObject.animFrame) ||
				(!currentBufferObject.key && this.replayAnimationFrames.Count < 1)) {
				return;
			}

			if (this.startAnimTime == -1f && this.firstFrameTime == -1f && currentBufferObject.key) {
				this.firstFrameTime = currentBufferObject.frameTime;
				this.startAnimTime = PlayTime.time;
			}

			while (this.replayAnimationFrames.Count > 30 * 120) {
				this.replayAnimationFrames.RemoveAt(0);
			}

			Vector3[] replayVectors = new Vector3[vectors.Length];
			Quaternion[] replayQuaternions = new Quaternion[quaternions.Length];
			vectors.CopyTo(replayVectors, 0);
			quaternions.CopyTo(replayQuaternions, 0);

			MultiplayerFrameBufferObject replayFrameObject = new MultiplayerFrameBufferObject {
				key = currentBufferObject.key,
				frameTime = currentBufferObject.frameTime,
				animFrame = currentBufferObject.animFrame,
				vectors = replayVectors,
				quaternions = replayQuaternions
			};

			currentBufferObject.replayFrameBufferObject = replayFrameObject;

			this.replayAnimationFrames.Add(replayFrameObject);
		}

		public void UnpackSounds(byte[] soundBytes) {
			int readBytes = 0;
			int numberOfAudioPlayers = MultiplayerUtils.audioPlayerNames.Count;

			List<AudioOneShotEvent>[] newOneShots = new List<AudioOneShotEvent>[numberOfAudioPlayers];
			List<AudioClipEvent>[] newClipEvents = new List<AudioClipEvent>[numberOfAudioPlayers];
			List<AudioVolumeEvent>[] newVolumeEvents = new List<AudioVolumeEvent>[numberOfAudioPlayers];
			List<AudioPitchEvent>[] newPitchEvents = new List<AudioPitchEvent>[numberOfAudioPlayers];
			List<AudioCutoffEvent>[] newCutoffEvents = new List<AudioCutoffEvent>[numberOfAudioPlayers];

			float earliestSoundTime = float.MaxValue;
			
			for (int i = 0; i < numberOfAudioPlayers; i++) {
				newOneShots[i] = new List<AudioOneShotEvent>();
				newClipEvents[i] = new List<AudioClipEvent>();
				newVolumeEvents[i] = new List<AudioVolumeEvent>();
				newPitchEvents[i] = new List<AudioPitchEvent>();
				newCutoffEvents[i] = new List<AudioCutoffEvent>();
				
				int oneShots = BitConverter.ToInt32(soundBytes, readBytes);
				readBytes += 4;
				for (int j = 0; j < oneShots; j++) {
					newOneShots[i].Add(new AudioOneShotEvent());
					newOneShots[i][j].clipName = MultiplayerUtils.ClipNameFromArrayByte(soundBytes[readBytes]);
					newOneShots[i][j].time = BitConverter.ToSingle(soundBytes, readBytes + 1);
					newOneShots[i][j].volumeScale = BitConverter.ToSingle(soundBytes, readBytes + 5) * Main.settings.volumeMultiplier;

					earliestSoundTime = Mathf.Min(newOneShots[i][j].time, earliestSoundTime);

					readBytes += 9;
				}
				
				int clipEvents = BitConverter.ToInt32(soundBytes, readBytes);
				readBytes += 4;
				for (int j = 0; j < clipEvents; j++) {
					newClipEvents[i].Add(new AudioClipEvent());
					newClipEvents[i][j].clipName = MultiplayerUtils.ClipNameFromArrayByte(soundBytes[readBytes]);
					newClipEvents[i][j].time = BitConverter.ToSingle(soundBytes, readBytes + 1);
					newClipEvents[i][j].isPlaying = soundBytes[readBytes + 5] == 1 ? true : false;

					earliestSoundTime = Mathf.Min(newClipEvents[i][j].time, earliestSoundTime);

					readBytes += 6;
				}
				
				int volumeEvents = BitConverter.ToInt32(soundBytes, readBytes);
				readBytes += 4;
				for (int j = 0; j < volumeEvents; j++) {
					newVolumeEvents[i].Add(new AudioVolumeEvent());
					newVolumeEvents[i][j].time = BitConverter.ToSingle(soundBytes, readBytes);
					newVolumeEvents[i][j].volume = BitConverter.ToSingle(soundBytes, readBytes + 4) * Main.settings.volumeMultiplier;

					earliestSoundTime = Mathf.Min(newVolumeEvents[i][j].time, earliestSoundTime);

					readBytes += 8;
				}
				
				int pitchEvents = BitConverter.ToInt32(soundBytes, readBytes);
				readBytes += 4;
				for (int j = 0; j < pitchEvents; j++) {
					newPitchEvents[i].Add(new AudioPitchEvent());
					newPitchEvents[i][j].time = BitConverter.ToSingle(soundBytes, readBytes);
					newPitchEvents[i][j].pitch = BitConverter.ToSingle(soundBytes, readBytes + 4);

					earliestSoundTime = Mathf.Min(newPitchEvents[i][j].time, earliestSoundTime);

					readBytes += 8;
				}
				
				int cutoffEvents = BitConverter.ToInt32(soundBytes, readBytes);
				readBytes += 4;
				for ( int j = 0; j < cutoffEvents; j++) {
					newCutoffEvents[i].Add(new AudioCutoffEvent());
					newCutoffEvents[i][j].time = BitConverter.ToSingle(soundBytes, readBytes);
					newCutoffEvents[i][j].cutoff = BitConverter.ToSingle(soundBytes, readBytes + 4);

					earliestSoundTime = Mathf.Min(newCutoffEvents[i][j].time, earliestSoundTime);

					readBytes += 8;
				}
			}

			MultiplayerSoundBufferObject newSoundBufferObject = new MultiplayerSoundBufferObject(numberOfAudioPlayers);
			soundQueue.Add(newSoundBufferObject);
			
			for (int i = 0; i < MultiplayerUtils.audioPlayerNames.Count; i++) {
				newSoundBufferObject.audioClipEvents[i] = newClipEvents[i];
				newSoundBufferObject.audioOneShots[i] = newOneShots[i];
				newSoundBufferObject.audioCutoffEvents[i] = newCutoffEvents[i];
				newSoundBufferObject.audioPitchEvents[i] = newPitchEvents[i];
				newSoundBufferObject.audioVolumeEvents[i] = newVolumeEvents[i];
			}

			try {
				MultiplayerFrameBufferObject firstRealTime = this.replayAnimationFrames.FirstOrDefault(f => f.realFrameTime != -1f);

				if (firstRealTime != null) {
					foreach (ReplayAudioEventPlayer audioPlayer in replayController.AudioEventPlayers) {
						if (!ReferenceEquals(audioPlayer, null)) {
							if (audioPlayer.clipEvents != null) MultiplayerUtils.RemoveAudioEventsOlderThanExcept(audioPlayer.clipEvents, firstRealTime.realFrameTime, 0);
							if (audioPlayer.cutoffEvents != null) MultiplayerUtils.RemoveAudioEventsOlderThanExcept(audioPlayer.cutoffEvents, firstRealTime.realFrameTime, 0);
							if (audioPlayer.oneShotEvents != null) MultiplayerUtils.RemoveAudioEventsOlderThanExcept(audioPlayer.oneShotEvents, firstRealTime.realFrameTime, 0);
							if (audioPlayer.pitchEvents != null) MultiplayerUtils.RemoveAudioEventsOlderThanExcept(audioPlayer.pitchEvents, firstRealTime.realFrameTime, 0);
							if (audioPlayer.volumeEvents != null) MultiplayerUtils.RemoveAudioEventsOlderThanExcept(audioPlayer.volumeEvents, firstRealTime.realFrameTime, 0);
						}
					}
				}
			} catch (Exception) { }
		}

		MultiplayerFrameBufferObject previousFinishedFrame = null;
		MultiplayerFrameBufferObject currentAnimationFrame = null;
		// TODO: refactor all this shit, I'm sure there's a better way
		public void LerpNextFrame(bool inReplay, bool recursive = false, float offset = 0, int recursionLevel = 0) {
			if (this.animationFrames.Count == 0 || this.animationFrames[0] == null) return;
			int animationFramesCount = this.animationFrames.Count;
			currentAnimationFrame = this.animationFrames[0];
			if (!startedAnimating && animationFrames.Count > 5) {
				if (currentAnimationFrame.vectors == null || !currentAnimationFrame.key) {
					this.animationFrames.RemoveAt(0);
					LerpNextFrame(inReplay, true, Time.unscaledDeltaTime, 0);
					return;
				}

				startedAnimating = true;

				for (int i = 0; i < 77; i++) {
					bones[i].localPosition = currentAnimationFrame.vectors[i];
					bones[i].localRotation = currentAnimationFrame.quaternions[i];
				}

				this.previousFrameTime = currentAnimationFrame.frameTime;
				this.previousFinishedFrame = currentAnimationFrame;
				//this.firstFrameTime = currentAnimationFrame.frameTime;
				//this.startAnimTime = Time.time;

				this.animationFrames.RemoveAt(0);
			}

			if (!startedAnimating) return;

			int frameDelay = 0;

			if (this.animationFrames.Count < 2) return;

			if (this.animationFrames.Count < 4 || this.waitingForDelay) {
				this.waitingForDelay = !(this.animationFrames.Count > 5);
			}

			if (this.animationFrames.Count >= 10 || this.speedDelay) {
				if (this.animationFrames.Count > 50) {
					this.animationFrames.RemoveRange(0, this.animationFrames.Count - 20);
				}

				this.speedDelay = !(this.animationFrames.Count < 7);
			}

			if (this.waitingForDelay || this.speedDelay) {
				frameDelay = this.animationFrames.Count - 6;
			}

			if (currentAnimationFrame.vectors == null) {
				this.animationFrames.RemoveAt(0);

				LerpNextFrame(inReplay, true, Time.unscaledDeltaTime, recursionLevel + 1);
				return;
			}

			if (currentAnimationFrame.deltaTime == 0) {
				currentAnimationFrame.deltaTime = currentAnimationFrame.frameTime - this.previousFrameTime;

				if (currentAnimationFrame.deltaTime == 0.0f) {
					currentAnimationFrame.deltaTime = 1f / 30f;
				}

				if (frameDelay != 0) {
					//debugWriter.Write("Adjusting current animation frame time from: " + currentAnimationFrame.deltaTime);
					currentAnimationFrame.deltaTime = frameDelay < 0 ? currentAnimationFrame.deltaTime * Mathf.Max(Mathf.Abs(frameDelay), 2) : currentAnimationFrame.deltaTime / Mathf.Min(Mathf.Max(frameDelay, 2), 6);
					//debugWriter.WriteLine("  To: " + currentAnimationFrame.deltaTime);

					if (currentAnimationFrame.deltaTime > 0.14f) {
						debugWriter.WriteLine("Capping current frame to 140ms");
						currentAnimationFrame.deltaTime = 0.14f;
					}
				}
			}

			if (!recursive) currentAnimationFrame.timeSinceStart += Time.unscaledDeltaTime;
			else currentAnimationFrame.timeSinceStart = offset;

			// TODO: figure out why this occassionally spikes frames
			if (!inReplay) {
				if (currentAnimationFrame.timeSinceStart < currentAnimationFrame.deltaTime) {
					for (int i = 0; i < 77; i++) {
						bones[i].localPosition = Vector3.Lerp(previousFinishedFrame.vectors[i], currentAnimationFrame.vectors[i], currentAnimationFrame.timeSinceStart / currentAnimationFrame.deltaTime);
						bones[i].localRotation = Quaternion.Slerp(previousFinishedFrame.quaternions[i], currentAnimationFrame.quaternions[i], currentAnimationFrame.timeSinceStart / currentAnimationFrame.deltaTime);
					}
				}

				replayController.ClipEndTime = PlayTime.time + 0.5f;

				if (!recursive) {
					foreach (ReplayAudioEventPlayer replayAudioEventPlayer in replayController.AudioEventPlayers) {
						if (replayAudioEventPlayer != null && !replayAudioEventPlayer.enabled) replayAudioEventPlayer.enabled = true;
						if (replayAudioEventPlayer != null) replayAudioEventPlayer.SetPlaybackTime(PlayTime.time, 1.0f);
					}
				}
			}

			if (this.currentlySetUsername != this.username) {
				this.usernameText.text = this.username;
				this.currentlySetUsername = this.username;
			}
			this.usernameObject.transform.position = this.skater.transform.position + this.skater.transform.up;
			this.usernameObject.transform.LookAt(mainCameraTransform);

			if (currentAnimationFrame.timeSinceStart >= currentAnimationFrame.deltaTime) {
				// 30FPS 120Seconds
				//if (!currentAnimationFrame.key) {
				//	if (this.recordedFrames.Count > 30 * 120) {
				//		this.recordedFrames.RemoveAt(0);
				//		this.recordedFrames.Add(new ReplayRecordedFrame(BufferToInfo(currentAnimationFrame), this.startAnimTime + currentAnimationFrame.frameTime - this.firstFrameTime));
				//	} else {
				//		this.recordedFrames.Add(new ReplayRecordedFrame(BufferToInfo(currentAnimationFrame), this.startAnimTime + currentAnimationFrame.frameTime - this.firstFrameTime));
				//	}
				//}
				previousFinishedFrame = this.animationFrames[0];
				if (previousFinishedFrame.replayFrameBufferObject == previousFinishedFrame) {
					previousFinishedFrame.replayFrameBufferObject = this.replayAnimationFrames.Find(f => f.animFrame == previousFinishedFrame.animFrame);
				}
				previousFinishedFrame.replayFrameBufferObject.realFrameTime = PlayTime.time;

				if (!recursive) {
					int soundsToRemove = 0;
					int soundQueueCount = soundQueue.Count;
					for (soundsToRemove = 0; soundsToRemove < soundQueueCount; soundsToRemove++) {
						if (previousFinishedFrame.replayFrameBufferObject.frameTime + 1 / 15f < soundQueue[0].playTime) {
							break;
						}

						if (soundsToRemove == soundQueueCount - 1) {
							soundsToRemove++;
							break;
						}
					}

					if (soundsToRemove > 0) {
						for (int i = 0; i < soundsToRemove; i++) {
							soundQueue[i].AdjustRealTimeToAnimation(previousFinishedFrame.replayFrameBufferObject);
							soundQueue[i].AddSoundsToPlayers(this.replayController, this.replayEventPlayerForName);
						}

						soundQueue.RemoveRange(0, soundsToRemove);
					}
				}

				for (int i = 0; i < 77; i++) {
					if (!inReplay) {
						bones[i].localPosition = previousFinishedFrame.vectors[i];
						bones[i].localRotation = previousFinishedFrame.quaternions[i];
					}

					if (!this.animationFrames[1].key) {
						this.animationFrames[1].vectors[i] += previousFinishedFrame.vectors[i];
						this.animationFrames[1].quaternions[i].eulerAngles = this.animationFrames[1].quaternions[i].eulerAngles + previousFinishedFrame.quaternions[i].eulerAngles;
					}
				}
				replayController.ClipEndTime = PlayTime.time + 0.5f;

				float oldTime = previousFinishedFrame.timeSinceStart;
				float oldDelta = previousFinishedFrame.deltaTime;

				this.previousFrameTime = previousFinishedFrame.frameTime;
				this.animationFrames.RemoveAt(0);

				if (oldTime - oldDelta > 0f && recursionLevel < 2) {
					this.LerpNextFrame(inReplay, true, oldTime - oldDelta, recursionLevel + 1);
					return;
				}
			}
		}
		
		TransformInfo[] BufferToInfo(MultiplayerFrameBufferObject frame) {
			return CreateInfoArray(frame.vectors, frame.quaternions);
		}

		TransformInfo[] CreateInfoArray(Vector3[] vectors, Quaternion[] quaternions) {
			TransformInfo[] info = new TransformInfo[replayController.playbackTransforms.Count];

			for (int i = 0; i < info.Length; i++) {
				info[i] = new TransformInfo(this.skater.transform);
				info[i].position = vectors[i];
				info[i].rotation = quaternions[i];
			}

			return info;
		}

		public void PrepareReplay() {
			this.recordedFrames.Clear();

			this.replayAnimationFrames = this.replayAnimationFrames.OrderBy(f => f.animFrame).ToList().ToList();

			if (replayAnimationFrames == null || replayAnimationFrames.Count == 0) return;

			int firstKey = this.replayAnimationFrames.FindIndex(f => f.key);

			if (firstKey < 0) return;
			
			this.replayAnimationFrames.RemoveRange(0, firstKey);

			MultiplayerFrameBufferObject firstRealTime, lastRealTime;
			firstRealTime = this.replayAnimationFrames.Find(f => f.realFrameTime != -1f);
			lastRealTime = this.replayAnimationFrames.Last(f => f.realFrameTime != -1f);

			if (firstRealTime == lastRealTime) return;

			float averageFrameTime = (lastRealTime.animFrame - firstRealTime.animFrame) / (lastRealTime.realFrameTime - firstRealTime.realFrameTime);

			ReplayRecordedFrame previousFrame = null;
			for(int f = 0; f < this.replayAnimationFrames.Count; f++) {
				MultiplayerFrameBufferObject frame = this.replayAnimationFrames[f];

				if(f == 0) {
					frame.realFrameTime = firstRealTime.realFrameTime + ((frame.animFrame - firstRealTime.animFrame) * 1f/30f);
				}

				//if(f > 0 && !frame.key && this.replayAnimationFrames[f - 1].animFrame != frame.animFrame - 1) {
				//	continue;
				//}

				TransformInfo[] transforms = null;

				if (previousFrame != null && !frame.key && this.replayAnimationFrames[f - 1].animFrame != frame.animFrame) {
					if (this.replayAnimationFrames[f - 1].animFrame != frame.animFrame - 1) {
						MultiplayerFrameBufferObject nextKeyFrame = this.replayAnimationFrames.FirstOrDefault(nK => nK.animFrame > frame.animFrame);
						int framesToInterpolate = frame.animFrame - this.replayAnimationFrames[f - 1].animFrame;
						if (nextKeyFrame != null && nextKeyFrame.realFrameTime != -1f) {
							int frameDiff = frame.animFrame - nextKeyFrame.animFrame;
							for (int i = 1; i < framesToInterpolate; i++) {
								Vector3[] interpolatedVectors = new Vector3[frame.vectors.Length];
								Quaternion[] interpolatedQuaternions = new Quaternion[frame.vectors.Length];
								for (int c = 0; c < frame.vectors.Length; c++) {
									interpolatedVectors[c] = Vector3.Lerp(this.replayAnimationFrames[f - 1].vectors[c], nextKeyFrame.vectors[c], c / frameDiff);
									interpolatedQuaternions[c] = Quaternion.Slerp(this.replayAnimationFrames[f - 1].quaternions[c], nextKeyFrame.quaternions[c], c / frameDiff);
								}

								float frameTime = this.replayAnimationFrames[f - 1].realFrameTime + (i / frameDiff) * (nextKeyFrame.frameTime - this.replayAnimationFrames[f - 1].realFrameTime);

								this.recordedFrames.Add(new ReplayRecordedFrame(CreateInfoArray(interpolatedVectors, interpolatedQuaternions), frameTime));

								previousFrame = this.recordedFrames.Last();
							}
						}
					}

					Vector3[] vectors = new Vector3[frame.vectors.Length];
					Quaternion[] quaternions = new Quaternion[frame.vectors.Length];
					for (int i = 0; i < frame.vectors.Length; i++) {
						vectors[i] = previousFrame.transformInfos[i].position + frame.vectors[i];
						quaternions[i].eulerAngles = previousFrame.transformInfos[i].rotation.eulerAngles + frame.quaternions[i].eulerAngles;
					}

					transforms = CreateInfoArray(vectors, quaternions);
				}else if (frame.key && (f != 0 && frame.animFrame != this.replayAnimationFrames[f - 1].animFrame)) {
					transforms = BufferToInfo(frame);
				}

				if (frame.realFrameTime == -1f && f > 0) {
					frame.realFrameTime = this.replayAnimationFrames[f - 1].realFrameTime + (frame.animFrame - this.replayAnimationFrames[f - 1].animFrame) * (1f / 30f);
				}

				if (transforms != null && frame.realFrameTime != -1f) {
					this.recordedFrames.Add(new ReplayRecordedFrame(transforms, frame.realFrameTime));

					previousFrame = this.recordedFrames.Last();
				}
			}

			Traverse.Create(replayController).Property("gameplayEvents").SetValue(new List<GPEvent>());

			FinalizeReplay(true);
		}

		public void FinalizeReplay(bool subtractStartTime = true) {
			this.replayController.enabled = true;
			this.recordedFrames = this.recordedFrames.OrderBy(f => f.time).ToList();
			this.EnsureQuaternionListContinuity();
			Traverse.Create(this.replayController).Property("ClipFrames").SetValue((from f in this.recordedFrames select f.Copy()).ToList());
			if (subtractStartTime) { // Subtract Start Time
				float firstFrameGameTime = ReplayRecorder.Instance.RecordedFrames[0].time;
				this.replayController.ClipFrames.ForEach(delegate (ReplayRecordedFrame f) {
					f.time -= firstFrameGameTime;
				});
				UnityModManagerNet.UnityModManager.Logger.Log(this.replayController.ClipFrames[0].time + " " + ReplayEditorController.Instance.playbackController.ClipFrames[0].time + " " + firstFrameGameTime);
				this.replayController.ClipEndTime = this.replayController.ClipFrames[this.replayController.ClipFrames.Count - 1].time;
			}
			this.replayController.StartCoroutine("UpdateAnimationClip");
			this.usernameObject.GetComponent<Renderer>().enabled = false;
		}

		public void EndReplay() {
			this.replayController.enabled = false;
			this.usernameObject.GetComponent<Renderer>().enabled = true;
			this.recordedFrames.Clear();
		}

		private void EnsureQuaternionListContinuity() {
			float[] array = Enumerable.Repeat<float>(0f, bones.Length).ToArray<float>();
			float[] array2 = Enumerable.Repeat<float>(0f, bones.Length).ToArray<float>();
			float[] array3 = Enumerable.Repeat<float>(0f, bones.Length).ToArray<float>();
			float[] array4 = Enumerable.Repeat<float>(0f, bones.Length).ToArray<float>();
			for (int i = 0; i < this.recordedFrames.Count; i++) {
				ReplayRecordedFrame replayRecordedFrame = this.recordedFrames[i];
				for (int j = 0; j < bones.Length; j++) {
					Quaternion rotation = replayRecordedFrame.transformInfos[j].rotation;
					float num = rotation.x;
					float num2 = rotation.y;
					float num3 = rotation.z;
					float num4 = rotation.w;
					if (array[j] * num + array2[j] * num2 + array3[j] * num3 + array4[j] * num4 < 0f) {
						num = -num;
						num2 = -num2;
						num3 = -num3;
						num4 = -num4;
						this.recordedFrames[i].transformInfos[j].rotation = new Quaternion(num, num2, num3, num4);
					}
					array[j] = num;
					array2[j] = num2;
					array3[j] = num3;
					array4[j] = num4;
				}
			}
		}

		public void Destroy() {
			GameObject.Destroy(skater);
			GameObject.Destroy(board);
			GameObject.Destroy(usernameObject);
			GameObject.Destroy(player);
		}
	}
}
