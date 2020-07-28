using HarmonyLib;
using ReplayEditor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerFrameBufferObject {
		public Vector3[] vectors = null;
		public Quaternion[] quaternions = null;

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
		}
	}

	public class MultiplayerSoundBufferObject {
		public List<List<AudioOneShotEvent>> audioOneShots = new List<List<AudioOneShotEvent>>();
		public List<List<AudioClipEvent>> audioClipEvents = new List<List<AudioClipEvent>>();
		public List<List<AudioVolumeEvent>> audioVolumeEvents = new List<List<AudioVolumeEvent>>();
		public List<List<AudioPitchEvent>> audioPitchEvents = new List<List<AudioPitchEvent>>();
		public List<List<AudioCutoffEvent>> audioCutoffEvents = new List<List<AudioCutoffEvent>>();

		public float playTime = 0f;
		public bool setRealTime = false;

		public void AdjustRealTimeToAnimation(MultiplayerFrameBufferObject animationFrame) {
			for (int i = 0; i < MultiplayerUtils.audioPlayerNames.Count; i++) {
				foreach (AudioOneShotEvent oneShot in audioOneShots[i]) {
					oneShot.time = animationFrame.realFrameTime + animationFrame.frameTime - oneShot.time;
				}
				foreach (AudioClipEvent clipEvent in audioClipEvents[i]) {
					clipEvent.time = animationFrame.realFrameTime + animationFrame.frameTime - clipEvent.time;
				}
				foreach (AudioVolumeEvent volumeEvent in audioVolumeEvents[i]) {
					volumeEvent.time = animationFrame.realFrameTime + animationFrame.frameTime - volumeEvent.time;
				}
				foreach (AudioPitchEvent pitchEvent in audioPitchEvents[i]) {
					pitchEvent.time = animationFrame.realFrameTime + animationFrame.frameTime - pitchEvent.time;
				}
				foreach (AudioCutoffEvent cutoffEvent in audioCutoffEvents[i]) {
					cutoffEvent.time = animationFrame.realFrameTime + animationFrame.frameTime - cutoffEvent.time;
				}
			}

			setRealTime = true;
		}

		public void AddSoundsToPlayers(ReplayPlaybackController replayController) {
			for (int i = 0; i < MultiplayerUtils.audioPlayerNames.Count; i++) {
				foreach (ReplayAudioEventPlayer audioPlayer in replayController.AudioEventPlayers) {
					if (audioPlayer.name.Equals(MultiplayerUtils.audioPlayerNames[i])) {
						if (audioOneShots[i] != null && audioOneShots[i].Count > 0 && replayController.AudioEventPlayers[i].oneShotEvents == null) {
							replayController.AudioEventPlayers[i].LoadOneShotEvents(audioOneShots[i]);
						} else if (audioOneShots[i] != null && audioOneShots[i].Count > 0) replayController.AudioEventPlayers[i].oneShotEvents.AddRange(audioOneShots[i]);

						if (audioClipEvents[i] != null && audioClipEvents[i].Count > 0 && replayController.AudioEventPlayers[i].clipEvents == null) {
							replayController.AudioEventPlayers[i].LoadClipEvents(audioClipEvents[i]);
						} else if (audioClipEvents[i] != null && audioClipEvents[i].Count > 0) replayController.AudioEventPlayers[i].clipEvents.AddRange(audioClipEvents[i]);

						if (audioVolumeEvents[i] != null && audioVolumeEvents[i].Count > 0 && replayController.AudioEventPlayers[i].volumeEvents == null) {
							replayController.AudioEventPlayers[i].LoadVolumeEvents(audioVolumeEvents[i]);
						} else if (audioVolumeEvents[i] != null && audioVolumeEvents[i].Count > 0) replayController.AudioEventPlayers[i].volumeEvents.AddRange(audioVolumeEvents[i]);

						if (audioPitchEvents[i] != null && audioPitchEvents[i].Count > 0 && replayController.AudioEventPlayers[i].pitchEvents == null) {
							replayController.AudioEventPlayers[i].LoadPitchEvents(audioPitchEvents[i]);
						} else if (audioPitchEvents[i] != null && audioPitchEvents[i].Count > 0) replayController.AudioEventPlayers[i].pitchEvents.AddRange(audioPitchEvents[i]);

						if (audioCutoffEvents[i] != null && audioCutoffEvents[i].Count > 0 && replayController.AudioEventPlayers[i].cutoffEvents == null) {
							replayController.AudioEventPlayers[i].LoadCutoffEvents(audioCutoffEvents[i]);
						} else if (audioCutoffEvents[i] != null && audioCutoffEvents[i].Count > 0) replayController.AudioEventPlayers[i].cutoffEvents.AddRange(audioCutoffEvents[i]);
					}
				}
			}
		}
	}

	public class MultiplayerRemotePlayerController : MultiplayerPlayerController {
		public GameObject skater { get; private set; }
		public GameObject board { get; private set; }

		public GameObject usernameObject;
		private TextMesh usernameText;

		private int currentAnimationPacket = -1;
		private Transform[] bones;

		private bool startedAnimating = false;
		private float previousFrameTime = 0;
		private float firstFrameTime = -1f;
		private float startAnimTime = -1f;
		public float lastReplayFrame = 1f;

		private bool waitingForDelay = false;
		private bool speedDelay = false;

		private List<byte> gearStream = new List<byte>();
		private bool completedGearStream = false;

		// TODO: Rename these to be easier to understand
		public List<MultiplayerFrameBufferObject> animationFrames = new List<MultiplayerFrameBufferObject>();
		public List<MultiplayerFrameBufferObject> replayAnimationFrames = new List<MultiplayerFrameBufferObject>();
		public List<ReplayRecordedFrame> recordedFrames = new List<ReplayRecordedFrame>();

		public List<MultiplayerSoundBufferObject> soundQueue = new List<MultiplayerSoundBufferObject>();

		public List<MultiplayerRemoteTexture> multiplayerTextures = new List<MultiplayerRemoteTexture>();
		public string bodyType = "male";

		public ReplayPlaybackController replayController;

		public byte playerID = 255;

		public MultiplayerRemotePlayerController(StreamWriter writer) : base(writer) {  }

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

			foreach (GearPrefabController gearPrefabController in this.player.GetComponentsInChildren<GearPrefabController>()) {
				GameObject.Destroy(gearPrefabController.gameObject);
			}

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

			completedGearStream = true;

			byte[] textureStream = gearStream.ToArray();

			ushort bodyTypeLen = BitConverter.ToUInt16(textureStream, 0);
			this.bodyType = Encoding.UTF8.GetString(textureStream, 2, bodyTypeLen);

			UnityModManagerNet.UnityModManager.Logger.Log($"Attempting to equip body with name length {bodyTypeLen} and name: {bodyType}!");

			CharacterBodyInfo bodyInfo = new CharacterBodyInfo("MP Temp body", this.bodyType, false, null, new string[0]);

			characterCustomizer.EquipGear(bodyInfo);

			int readBytes = 2 + bodyTypeLen;
			while (readBytes < textureStream.Length) {
				bool customTex = textureStream[readBytes] == 1 ? true : false;
				GearInfoType texInfotype = (GearInfoType)textureStream[readBytes + 1];

				readBytes += 2;
				if (customTex) {
					ushort typeLen = BitConverter.ToUInt16(textureStream, readBytes);
					int dataLen = BitConverter.ToInt32(textureStream, readBytes + 2);

					string texType = Encoding.UTF8.GetString(textureStream, readBytes + 6, typeLen);
					readBytes += 6 + typeLen;

					byte[] texData = new byte[dataLen];
					Array.Copy(textureStream, readBytes, texData, 0, dataLen);
					readBytes += dataLen;

					MultiplayerRemoteTexture newTexture = new MultiplayerRemoteTexture(customTex, "", texType, texInfotype, this.debugWriter);
					multiplayerTextures.Add(newTexture);
					newTexture.SaveTexture(this.playerID, texData);
				}
			}
		}

		public void ApplyTextures() {
			foreach (MultiplayerRemoteTexture mpTex in multiplayerTextures) {
				if (mpTex.saved && !mpTex.loaded && !(mpTex.textureType.Equals("head", StringComparison.InvariantCultureIgnoreCase) || mpTex.textureType.Equals("body", StringComparison.InvariantCultureIgnoreCase))) {
					mpTex.LoadFromFileMainThread(this);
				} else if (mpTex.textureType.Equals("head", StringComparison.InvariantCultureIgnoreCase) && mpTex.saved && !mpTex.loaded) {
					MultiplayerRemoteTexture bodyTex = multiplayerTextures.Find((t) => t.textureType.Equals("body", StringComparison.InvariantCultureIgnoreCase));
					if (bodyTex != null) {
						UnityModManagerNet.UnityModManager.Logger.Log($"Updating body textures for {this.bodyType} id: {this.playerID}");

						bodyTex.loaded = true;
						mpTex.loaded = true;
						
						TextureChange[] headChange = { new TextureChange("albedo", mpTex.fileLocation) };
						TextureChange[] bodyChange = { new TextureChange("albedo", bodyTex.fileLocation) };

						List<MaterialChange> materialChanges = new List<MaterialChange>();
						materialChanges.Add(new MaterialChange("body", bodyChange));
						materialChanges.Add(new MaterialChange("head", headChange));

						CharacterBodyInfo bodyInfo = new CharacterBodyInfo("MP Temp body", this.bodyType, true, materialChanges, new string[0]);

						characterCustomizer.EquipGear(bodyInfo);
					}
				}
			}
		}

		public void SetPlayerTexture(string path, string texType, GearInfoType infoType, bool useFull) {
			TextureChange texChange = new TextureChange("albedo", path);
			GearInfo newInfo;
			
			if(infoType == GearInfoType.Board) {
				newInfo = new BoardGearInfo("MP Temp " + texType.ToString(), texType, true, new TextureChange[] { texChange }, new string[0]);
			} else {
				newInfo = new CharacterGearInfo("MP Temp " + texType.ToString(), texType, true, new TextureChange[] { texChange }, new string[0]);
			}
			
			characterCustomizer.EquipGear(newInfo);
		}

		public void UnpackAnimations(byte[] recBuffer) {
			int receivedPacketSequence = BitConverter.ToInt32(recBuffer, 0);

			byte[] buffer = new byte[recBuffer.Length - 5];
			bool ordered = true;
			if (receivedPacketSequence < currentAnimationPacket - 5) {
				ordered = false;
			} else {
				Array.Copy(recBuffer, 5, buffer, 0, recBuffer.Length - 5);
				currentAnimationPacket = Math.Max(currentAnimationPacket, receivedPacketSequence);
			}

			MultiplayerFrameBufferObject currentBufferObject = new MultiplayerFrameBufferObject();

			currentBufferObject.key = recBuffer[4] == (byte)1 ? true : false;
			currentBufferObject.animFrame = receivedPacketSequence;

			currentBufferObject.frameTime = BitConverter.ToSingle(buffer, buffer.Length - 4);

			if (this.firstFrameTime != -1f && currentBufferObject.frameTime < this.firstFrameTime) {
				return;
			}

			List<Vector3> vectors = new List<Vector3>();
			List<Quaternion> quaternions = new List<Quaternion>();

			for (int i = 0; i < 77; i++) {
				if (currentBufferObject.key) {
					Vector3 readVector = new Vector3();
					readVector.x = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 12));
					readVector.y = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 12 + 2));
					readVector.z = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 12 + 4));

					Quaternion readQuaternion = new Quaternion();
					readQuaternion.eulerAngles = new Vector3(SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 12 + 6)),
													SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 12 + 8)),
													SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 12 + 10)));

					vectors.Add(readVector);
					quaternions.Add(readQuaternion);
				} else {
					Vector3 readVector = new Vector3();
					readVector.x = BitConverter.ToSingle(buffer, i * 24);
					readVector.y = BitConverter.ToSingle(buffer, i * 24 + 4);
					readVector.z = BitConverter.ToSingle(buffer, i * 24 + 8);

					Quaternion readQuaternion = new Quaternion();
					readQuaternion.eulerAngles = new Vector3(BitConverter.ToSingle(buffer, i * 24 + 12),
													BitConverter.ToSingle(buffer, i * 24 + 16),
													BitConverter.ToSingle(buffer, i * 24 + 20));

					vectors.Add(readVector);
					quaternions.Add(readQuaternion);
				}
			}

			currentBufferObject.vectors = vectors.ToArray();
			currentBufferObject.quaternions = quaternions.ToArray();

			if (ordered && this.animationFrames.Find(f => f.animFrame == currentBufferObject.animFrame) == null) {
				this.animationFrames.Add(currentBufferObject);
				this.animationFrames = this.animationFrames.OrderBy(f => f.animFrame).ToList();
			}

			if ((this.replayAnimationFrames.Count > 0 && this.replayAnimationFrames[0] != null && this.replayAnimationFrames[0].animFrame > currentBufferObject.animFrame) ||
				(this.replayAnimationFrames.Count > 0 && this.replayAnimationFrames.Find(f => f.animFrame == currentBufferObject.animFrame) != null) ||
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

			MultiplayerFrameBufferObject replayFrameObject = new MultiplayerFrameBufferObject {
				key = currentBufferObject.key,
				frameTime = currentBufferObject.frameTime,
				animFrame = currentBufferObject.animFrame,
				vectors = new Vector3[currentBufferObject.vectors.Length],
				quaternions = new Quaternion[currentBufferObject.quaternions.Length]
			};
			currentBufferObject.vectors.CopyTo(replayFrameObject.vectors, 0);
			currentBufferObject.quaternions.CopyTo(replayFrameObject.quaternions, 0);

			this.replayAnimationFrames.Add(replayFrameObject);
		}

		public void UnpackSounds(byte[] soundBytes) {
			int readBytes = 0;

			List<List<AudioOneShotEvent>> newOneShots = new List<List<AudioOneShotEvent>>();
			List<List<AudioClipEvent>> newClipEvents = new List<List<AudioClipEvent>>();
			List<List<AudioVolumeEvent>> newVolumeEvents = new List<List<AudioVolumeEvent>>();
			List<List<AudioPitchEvent>> newPitchEvents = new List<List<AudioPitchEvent>>();
			List<List<AudioCutoffEvent>> newCutoffEvents = new List<List<AudioCutoffEvent>>();

			float earliestSoundTime = float.MaxValue;
			
			for (int i = 0; i < MultiplayerUtils.audioPlayerNames.Count; i++) {
				newOneShots.Add(new List<AudioOneShotEvent>());
				newClipEvents.Add(new List<AudioClipEvent>());
				newVolumeEvents.Add(new List<AudioVolumeEvent>());
				newPitchEvents.Add(new List<AudioPitchEvent>());
				newCutoffEvents.Add(new List<AudioCutoffEvent>());
				
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

			MultiplayerSoundBufferObject newSoundBufferObject = new MultiplayerSoundBufferObject();
			soundQueue.Add(newSoundBufferObject);
			
			for (int i = 0; i < MultiplayerUtils.audioPlayerNames.Count; i++) {
				newSoundBufferObject.audioClipEvents.Add(new List<AudioClipEvent>());
				newSoundBufferObject.audioOneShots.Add(new List<AudioOneShotEvent>());
				newSoundBufferObject.audioCutoffEvents.Add(new List<AudioCutoffEvent>());
				newSoundBufferObject.audioPitchEvents.Add(new List<AudioPitchEvent>());
				newSoundBufferObject.audioVolumeEvents.Add(new List<AudioVolumeEvent>());
				
				newSoundBufferObject.audioClipEvents[i] = newClipEvents[i];
				newSoundBufferObject.audioOneShots[i] = newOneShots[i];
				newSoundBufferObject.audioCutoffEvents[i] = newCutoffEvents[i];
				newSoundBufferObject.audioPitchEvents[i] = newPitchEvents[i];
				newSoundBufferObject.audioVolumeEvents[i] = newVolumeEvents[i];
			}

			try {
				MultiplayerFrameBufferObject firstRealTime = this.replayAnimationFrames.First(f => f.realFrameTime != -1f);

				foreach (ReplayAudioEventPlayer audioPlayer in replayController.AudioEventPlayers) {
					if (audioPlayer != null) {
						if (audioPlayer.clipEvents != null) audioPlayer.clipEvents.RemoveEventsOlderThanExcept(firstRealTime.realFrameTime, 0);
						if (audioPlayer.cutoffEvents != null) audioPlayer.cutoffEvents.RemoveEventsOlderThanExcept(firstRealTime.realFrameTime, 0);
						if (audioPlayer.oneShotEvents != null) audioPlayer.oneShotEvents.RemoveEventsOlderThanExcept(firstRealTime.realFrameTime, 0);
						if (audioPlayer.pitchEvents != null) audioPlayer.pitchEvents.RemoveEventsOlderThanExcept(firstRealTime.realFrameTime, 0);
						if (audioPlayer.volumeEvents != null) audioPlayer.volumeEvents.RemoveEventsOlderThanExcept(firstRealTime.realFrameTime, 0);
					}
				}
			} catch (Exception) { }
		}

		// TODO: refactor all this shit, I'm sure there's a better way
		public void LerpNextFrame(bool inReplay, bool recursive = false, float offset = 0, int recursionLevel = 0) {
			if (this.animationFrames.Count == 0 || this.animationFrames[0] == null) return;
			if (!startedAnimating && animationFrames.Count > 5) {
				if (this.animationFrames[0].vectors == null || !this.animationFrames[0].key) {
					this.animationFrames.RemoveAt(0);
					LerpNextFrame(inReplay);
				}

				if (this.animationFrames.Count > 6)
					this.animationFrames.RemoveRange(0, (this.animationFrames.Count - 6));

				startedAnimating = true;

				for (int i = 0; i < 77; i++) {
					bones[i].localPosition = this.animationFrames[0].vectors[i];
					bones[i].localRotation = this.animationFrames[0].quaternions[i];
				}

				this.previousFrameTime = this.animationFrames[0].frameTime;
				//this.firstFrameTime = this.animationFrames[0].frameTime;
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

			if (this.animationFrames[0].vectors == null) {
				this.animationFrames.RemoveAt(0);

				LerpNextFrame(inReplay);
			}

			if (this.animationFrames[0].deltaTime == 0) {
				this.animationFrames[0].deltaTime = this.animationFrames[0].frameTime - this.previousFrameTime;

				if (this.animationFrames[0].deltaTime == 0.0f) {
					this.animationFrames[0].deltaTime = 1f / 30f;
				}

				if (frameDelay != 0) {
					debugWriter.Write("Adjusting current animation frame time from: " + this.animationFrames[0].deltaTime);
					this.animationFrames[0].deltaTime = frameDelay < 0 ? this.animationFrames[0].deltaTime * Mathf.Max(Mathf.Abs(frameDelay), 2) : this.animationFrames[0].deltaTime / Mathf.Min(Mathf.Max(frameDelay, 2), 6);
					debugWriter.WriteLine("  To: " + this.animationFrames[0].deltaTime);

					if (this.animationFrames[0].deltaTime > 0.14f) {
						debugWriter.WriteLine("Capping current frame to 140ms");
						this.animationFrames[0].deltaTime = 0.14f;
					}
				}
			}

			if (!recursive) this.animationFrames[0].timeSinceStart += Time.unscaledDeltaTime;
			else this.animationFrames[0].timeSinceStart = offset;

			if (!inReplay) {
				for (int i = 0; i < 77; i++) {
					bones[i].localPosition = Vector3.Lerp(bones[i].localPosition, this.animationFrames[0].vectors[i], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
					bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, this.animationFrames[0].quaternions[i], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
				}

				replayController.ClipEndTime = PlayTime.time + 0.5f;

				foreach (ReplayAudioEventPlayer replayAudioEventPlayer in replayController.AudioEventPlayers) {
					if (replayAudioEventPlayer != null && !replayAudioEventPlayer.enabled) replayAudioEventPlayer.enabled = true;
					if (replayAudioEventPlayer != null) replayAudioEventPlayer.SetPlaybackTime(PlayTime.time, 1.0f);
				}
			}

			this.player.transform.position = PlayerController.Instance.transform.position;
			this.player.transform.rotation = PlayerController.Instance.transform.rotation;

			this.usernameText.text = this.username;
			this.usernameObject.transform.position = this.skater.transform.position + this.skater.transform.up;
			this.usernameObject.transform.LookAt(Camera.main.transform);

			if (this.animationFrames[0].timeSinceStart >= this.animationFrames[0].deltaTime) {
				// 30FPS 120Seconds
				//if (!this.animationFrames[0].key) {
				//	if (this.recordedFrames.Count > 30 * 120) {
				//		this.recordedFrames.RemoveAt(0);
				//		this.recordedFrames.Add(new ReplayRecordedFrame(BufferToInfo(this.animationFrames[0]), this.startAnimTime + this.animationFrames[0].frameTime - this.firstFrameTime));
				//	} else {
				//		this.recordedFrames.Add(new ReplayRecordedFrame(BufferToInfo(this.animationFrames[0]), this.startAnimTime + this.animationFrames[0].frameTime - this.firstFrameTime));
				//	}
				//}
				MultiplayerFrameBufferObject currentPlayingFrame = this.replayAnimationFrames.Find(f => f.animFrame == this.animationFrames[0].animFrame);
				currentPlayingFrame.realFrameTime = PlayTime.time;

				while (soundQueue.Count > 0) {
					if (currentPlayingFrame.frameTime + 1/15f < soundQueue[0].playTime) {
						break;
					} else {
						soundQueue[0].AdjustRealTimeToAnimation(currentPlayingFrame);
						soundQueue[0].AddSoundsToPlayers(this.replayController);
						soundQueue.RemoveAt(0);
					}
				}

				if (!inReplay) {
					for (int i = 0; i < 77; i++) {
						bones[i].localPosition = this.animationFrames[0].vectors[i];
						bones[i].localRotation = this.animationFrames[0].quaternions[i];
					}

					replayController.ClipEndTime = PlayTime.time + 0.5f;

					foreach (ReplayAudioEventPlayer replayAudioEventPlayer in replayController.AudioEventPlayers) {
						if (replayAudioEventPlayer != null && !replayAudioEventPlayer.enabled) replayAudioEventPlayer.enabled = true;
						if (replayAudioEventPlayer != null) replayAudioEventPlayer.SetPlaybackTime(PlayTime.time, 1.0f);
					}
				}

				if (!this.animationFrames[1].key) {
					for (int i = 0; i < 77; i++) {
						this.animationFrames[1].vectors[i] = this.animationFrames[0].vectors[i] + this.animationFrames[1].vectors[i];
						this.animationFrames[1].quaternions[i].eulerAngles = this.animationFrames[0].quaternions[i].eulerAngles + this.animationFrames[1].quaternions[i].eulerAngles;
					}
				}

				float oldTime = this.animationFrames[0].timeSinceStart;
				float oldDelta = this.animationFrames[0].deltaTime;

				this.previousFrameTime = this.animationFrames[0].frameTime;
				this.animationFrames.RemoveAt(0);
				if (recursionLevel < 4) {
					this.LerpNextFrame(inReplay, true, oldTime - oldDelta, recursionLevel + 1);
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

			this.replayAnimationFrames = this.replayAnimationFrames.OrderBy(f => f.animFrame).ToList();

			int firstKey = this.replayAnimationFrames.FindIndex(f => f.key);
			
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

				if (previousFrame != null && !frame.key) {
					Vector3[] vectors = new Vector3[frame.vectors.Length];
					Quaternion[] quaternions = new Quaternion[frame.vectors.Length];
					for(int i = 0; i < frame.vectors.Length; i++) {
						vectors[i] = previousFrame.transformInfos[i].position + frame.vectors[i];
						quaternions[i].eulerAngles = previousFrame.transformInfos[i].rotation.eulerAngles + frame.quaternions[i].eulerAngles;
					}

					transforms = CreateInfoArray(vectors, quaternions);
				}else if (frame.key) {
					transforms = BufferToInfo(frame);
				}

				if(frame.realFrameTime == -1f && f > 0) {
					frame.realFrameTime = this.replayAnimationFrames[f - 1].realFrameTime + (frame.animFrame - this.replayAnimationFrames[f - 1].animFrame) * (1f / 30f);
				}

				if(transforms != null) {
					this.recordedFrames.Add(new ReplayRecordedFrame(transforms, frame.realFrameTime));

					previousFrame = this.recordedFrames.Last();
				}
			}

			FinalizeReplay();
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
