using Harmony12;
using ReplayEditor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerLocalPlayerController : MultiplayerPlayerController {
		public MultiplayerLocalTexture shirtMPTex;
		public MultiplayerLocalTexture pantsMPTex;
		public MultiplayerLocalTexture shoesMPTex;
		public MultiplayerLocalTexture hatMPTex;
		public MultiplayerLocalTexture deckMPTex;
		public MultiplayerLocalTexture gripMPTex;
		public MultiplayerLocalTexture wheelMPTex;
		public MultiplayerLocalTexture truckMPTex;
		public MultiplayerLocalTexture headMPTex;
		public MultiplayerLocalTexture bodyMPTex;

		private bool startedEncoding = false;

		private int framesSinceKey = 5;

		public float currentAnimationTime { get; private set; }

		private bool sentTextures = false;

		public MultiplayerLocalPlayerController(StreamWriter writer) : base(writer) {
			currentAnimationTime = 0f;
		}

		private System.Collections.IEnumerator IncrementLoading() {
			Main.utilityMenu.loadingStatus++;
			yield return new WaitForEndOfFrame();
		}

		public System.Collections.IEnumerator EncodeTextures() {
			if (!this.startedEncoding) {
				this.startedEncoding = true;
				Main.utilityMenu.isLoading = true;
				Main.utilityMenu.loadingStatus = 0;
				yield return new WaitForEndOfFrame();

				this.shirtMPTex.ConvertTexture();
				IncrementLoading();

				this.pantsMPTex.ConvertTexture();
				IncrementLoading();

				this.shoesMPTex.ConvertTexture();
				IncrementLoading();

				this.hatMPTex.ConvertTexture(512);
				IncrementLoading();

				this.deckMPTex.ConvertTexture();
				IncrementLoading();

				this.gripMPTex.ConvertTexture();
				IncrementLoading();

				this.truckMPTex.ConvertTexture(512);
				IncrementLoading();

				this.wheelMPTex.ConvertTexture(512);
				IncrementLoading();

				this.headMPTex.ConvertTexture(2048);
				IncrementLoading();

				this.bodyMPTex.ConvertTexture(2048);
				IncrementLoading();

				IncrementLoading();
				yield return new WaitForEndOfFrame();

				yield return new WaitForEndOfFrame();

				Main.utilityMenu.isLoading = false;
			}
			yield break;
		}

		public void SendTextures() {
			if (!Main.utilityMenu.isLoading && startedEncoding && !sentTextures && Main.multiplayerController.isFileConnected) {
				sentTextures = true;

				Main.multiplayerController.SendBytesRaw(this.shirtMPTex.GetSendData(), true, false, false, true);
				Main.multiplayerController.SendBytesRaw(this.pantsMPTex.GetSendData(), true, false, false, true);
				Main.multiplayerController.SendBytesRaw(this.shoesMPTex.GetSendData(), true, false, false, true);
				Main.multiplayerController.SendBytesRaw(this.hatMPTex.GetSendData(), true, false, false, true);

				Main.multiplayerController.SendBytesRaw(this.deckMPTex.GetSendData(), true, false, false, true);
				Main.multiplayerController.SendBytesRaw(this.gripMPTex.GetSendData(), true, false, false, true);
				Main.multiplayerController.SendBytesRaw(this.truckMPTex.GetSendData(), true, false, false, true);
				Main.multiplayerController.SendBytesRaw(this.wheelMPTex.GetSendData(), true, false, false, true);

				Main.multiplayerController.SendBytesRaw(this.headMPTex.GetSendData(), true, false, false, true);
				Main.multiplayerController.SendBytesRaw(this.bodyMPTex.GetSendData(), true, false, false, true);
			}
		}

		public override void ConstructPlayer() {
			this.debugWriter.WriteLine("Constructing Local Player");

			this.player = PlayerController.Instance.skaterController.skaterTransform.gameObject;
			this.debugWriter.WriteLine("Found player");
			
			this.debugWriter.WriteLine("All clothing ID's");
			foreach(KeyValuePair<string, CharacterGearTemplate> pair in GearDatabase.Instance.CharGearTemplateForID) {
				this.debugWriter.WriteLine(pair.Key);
			}

			shirtMPTex = new MultiplayerLocalTexture(MPTextureType.Shirt, this.debugWriter);
			pantsMPTex = new MultiplayerLocalTexture(MPTextureType.Pants, this.debugWriter);
			shoesMPTex = new MultiplayerLocalTexture(MPTextureType.Shoes, this.debugWriter);
			hatMPTex = new MultiplayerLocalTexture(MPTextureType.Hat, this.debugWriter);
			deckMPTex = new MultiplayerLocalTexture(MPTextureType.Deck, this.debugWriter);
			gripMPTex = new MultiplayerLocalTexture(MPTextureType.Grip, this.debugWriter);
			wheelMPTex = new MultiplayerLocalTexture(MPTextureType.Wheels, this.debugWriter);
			truckMPTex = new MultiplayerLocalTexture(MPTextureType.Trucks, this.debugWriter);
			headMPTex = new MultiplayerLocalTexture(MPTextureType.Head, this.debugWriter);
			bodyMPTex = new MultiplayerLocalTexture(MPTextureType.Body, this.debugWriter);

			foreach (ClothingGearObjet clothingPiece in gearList) {
				// Get the path of the gear piece
				string path = "";
				bool custom = clothingPiece.gearInfo.isCustom;
				foreach(TextureChange change in clothingPiece.gearInfo.textureChanges) {
					if (change.textureID.ToLower().Equals("albedo")) {
						path = change.texturePath;
					}
				}

				switch (clothingPiece.template.categoryName.ToLower()) {
					case "shirt":
						this.shirtMPTex = new MultiplayerLocalTexture(custom, path, MPTextureType.Shirt, this.debugWriter) {
							useFull = false
						};
						break;
					case "hoodie":
						this.shirtMPTex = new MultiplayerLocalTexture(custom, path, MPTextureType.Shirt, this.debugWriter) {
							useFull = true
						};
						break;
					case "hat":
						this.hatMPTex = new MultiplayerLocalTexture(custom, path, MPTextureType.Hat, this.debugWriter);
						break;
					case "pants":
						this.pantsMPTex = new MultiplayerLocalTexture(custom, path, MPTextureType.Pants, this.debugWriter);
						break;
					case "shoes":
						this.shoesMPTex = new MultiplayerLocalTexture(custom, path, MPTextureType.Shoes, this.debugWriter);
						break;
				}
			}

			foreach(BoardGearObject boardGear in boardGearList) {
				// Get the path of the gear piece
				string path = "";
				bool custom = boardGear.gearInfo.isCustom;
				foreach (TextureChange change in boardGear.gearInfo.textureChanges) {
					if (change.textureID.ToLower().Equals("albedo")) {
						path = change.texturePath;
					}
				}
				
				string gearType = BoardGearObject.AdaptMaterialID(boardGear.gearInfo.type);
				
				switch (gearType) {
					case "deck":
						this.deckMPTex = new MultiplayerLocalTexture(custom, path, MPTextureType.Deck, this.debugWriter);
						break;
					case "griptape":
						this.gripMPTex = new MultiplayerLocalTexture(custom, path, MPTextureType.Grip, this.debugWriter);
						break;
					case "truck":
						this.truckMPTex = new MultiplayerLocalTexture(custom, path, MPTextureType.Trucks, this.debugWriter);
						break;
					case "wheel":
						this.wheelMPTex = new MultiplayerLocalTexture(custom, path, MPTextureType.Wheels, this.debugWriter);
						break;
				}
			}
			
			/* Get the body textures */
			{
				string pathHead = "";
				string pathBody = "";
				bool isCustom = currentBody.gearInfo.isCustom;

				this.debugWriter.WriteLine("Body changes\n{0}", currentBody.gearInfo.type);
				if(currentBody.gearInfo.tags != null) {
					this.debugWriter.WriteLine("Tags length {0}", currentBody.gearInfo.tags.Length);
				}

				foreach (MaterialChange matChange in currentBody.gearInfo.materialChanges) {
					this.debugWriter.WriteLine("Mat ID: {0}", matChange.materialID);
					foreach (TextureChange change in matChange.textureChanges) {
						this.debugWriter.WriteLine("tex ID: {0}", change.textureID);
						if (change.textureID.ToLower().Equals("albedo")) {
							if (matChange.materialID.ToLower().Equals("head")) {
								pathHead = change.texturePath;
							} else {
								pathBody = change.texturePath;
							}
						}
					}
				}

				this.headMPTex = new MultiplayerLocalTexture(isCustom, pathHead, MPTextureType.Head, this.debugWriter);
				this.bodyMPTex = new MultiplayerLocalTexture(isCustom, pathBody, MPTextureType.Body, this.debugWriter);
			}
			
			for (int i = 0; i < MultiplayerUtils.audioPlayerNames.Count; i++) {
				if (lastSentOneShots.Count < i + 1)
					lastSentOneShots.Add(null);
				if (lastSentClipEvents.Count < i + 1)
					lastSentClipEvents.Add(null);
				if (lastSentVolumeEvents.Count < i + 1)
					lastSentVolumeEvents.Add(null);
				if (lastSentPitchEvents.Count < i + 1)
					lastSentPitchEvents.Add(null);
				if (lastSentCutoffEvents.Count < i + 1)
					lastSentCutoffEvents.Add(null);

				lastSentOneShots[i] = ReplayRecorder.Instance.oneShotEvents != null && ReplayRecorder.Instance.oneShotEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) && ReplayRecorder.Instance.oneShotEvents[MultiplayerUtils.audioPlayerNames[i]].Count > 0 ? ReplayRecorder.Instance.oneShotEvents[MultiplayerUtils.audioPlayerNames[i]][ReplayRecorder.Instance.oneShotEvents[MultiplayerUtils.audioPlayerNames[i]].Count - 1] : null;
				lastSentClipEvents[i] = ReplayRecorder.Instance.clipEvents != null && ReplayRecorder.Instance.clipEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) && ReplayRecorder.Instance.clipEvents[MultiplayerUtils.audioPlayerNames[i]].Count > 0 ? ReplayRecorder.Instance.clipEvents[MultiplayerUtils.audioPlayerNames[i]][ReplayRecorder.Instance.clipEvents[MultiplayerUtils.audioPlayerNames[i]].Count - 1] : null;
				lastSentVolumeEvents[i] = ReplayRecorder.Instance.volumeEvents != null && ReplayRecorder.Instance.volumeEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) && ReplayRecorder.Instance.volumeEvents[MultiplayerUtils.audioPlayerNames[i]].Count > 0 ? ReplayRecorder.Instance.volumeEvents[MultiplayerUtils.audioPlayerNames[i]][ReplayRecorder.Instance.volumeEvents[MultiplayerUtils.audioPlayerNames[i]].Count - 1] : null;
				lastSentPitchEvents[i] = ReplayRecorder.Instance.pitchEvents != null && ReplayRecorder.Instance.pitchEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) && ReplayRecorder.Instance.pitchEvents[MultiplayerUtils.audioPlayerNames[i]].Count > 0 ? ReplayRecorder.Instance.pitchEvents[MultiplayerUtils.audioPlayerNames[i]][ReplayRecorder.Instance.pitchEvents[MultiplayerUtils.audioPlayerNames[i]].Count - 1] : null;
				lastSentCutoffEvents[i] = ReplayRecorder.Instance.cutoffEvents != null && ReplayRecorder.Instance.cutoffEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) && ReplayRecorder.Instance.cutoffEvents[MultiplayerUtils.audioPlayerNames[i]].Count > 0 ? ReplayRecorder.Instance.cutoffEvents[MultiplayerUtils.audioPlayerNames[i]][ReplayRecorder.Instance.cutoffEvents[MultiplayerUtils.audioPlayerNames[i]].Count - 1] : null;
			}

			this.debugWriter.WriteLine("Finished constructing local player");
		}

		public byte[] PackTransformInfoArray(List<ReplayRecordedFrame> frames, int start, bool useKey) {
			if(frames.Count < 2) {
				return null;
			}
			TransformInfo[] T = frames[frames.Count - 1].transformInfos;
			TransformInfo[] TPrevious = frames[frames.Count - 2].transformInfos;

			this.currentAnimationTime = frames[frames.Count - 1].time;

			byte[] packed = new byte[useKey ? T.Length * 12 - (start * 12) : T.Length * 24 - (start * 24)];

			for (int i = 0; i < T.Length - start; i++) {
				float x = useKey ? T[i + start].position.x : T[i + start].position.x - TPrevious[i + start].position.x;
				float y = useKey ? T[i + start].position.y : T[i + start].position.y - TPrevious[i + start].position.y;
				float z = useKey ? T[i + start].position.z : T[i + start].position.z - TPrevious[i + start].position.z;

				Vector3 rotationVec = T[i + start].rotation.eulerAngles;
				Vector3 prevRotVec = TPrevious[i + start].rotation.eulerAngles;
				float rx = useKey ? rotationVec.x : rotationVec.x - prevRotVec.x;
				float ry = useKey ? rotationVec.y : rotationVec.y - prevRotVec.y;
				float rz = useKey ? rotationVec.z : rotationVec.z - prevRotVec.z;

				if (!useKey && GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.ReplayState)) {
					x = 0f;
					y = 0f;
					z = 0f;
					rx = 0f;
					ry = 0f;
					rz = 0f;
				}

				if (!useKey) {
					Array.Copy(BitConverter.GetBytes(x), 0, packed, i * 24, 4);
					Array.Copy(BitConverter.GetBytes(y), 0, packed, i * 24 + 4, 4);
					Array.Copy(BitConverter.GetBytes(z), 0, packed, i * 24 + 8, 4);

					Array.Copy(BitConverter.GetBytes(rx), 0, packed, i * 24 + 12, 4);
					Array.Copy(BitConverter.GetBytes(ry), 0, packed, i * 24 + 16, 4);
					Array.Copy(BitConverter.GetBytes(rz), 0, packed, i * 24 + 20, 4);
				} else {
					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(x)), 0, packed, i * 12, 2);
					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(y)), 0, packed, i * 12 + 2, 2);
					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(z)), 0, packed, i * 12 + 4, 2);

					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(rx)), 0, packed, i * 12 + 6, 2);
					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(ry)), 0, packed, i * 12 + 8, 2);
					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(rz)), 0, packed, i * 12 + 10, 2);
				}
			}

			return packed;
		}

		public Tuple<byte[], bool> PackAnimations() {
			framesSinceKey++;
			bool useKey = false;

			// Use key frame every 5 frames
			if(framesSinceKey >= 5) {
				useKey = true;
				framesSinceKey = 0;
			}

			byte[] transforms = PackTransformInfoArray(ReplayRecorder.Instance.RecordedFrames, 0, useKey);

			if (transforms == null) return null;

			byte[] packed = new byte[transforms.Length + 5];

			packed[0] = useKey ? (byte)1: (byte)0;
			Array.Copy(transforms, 0, packed, 1, transforms.Length);
			Array.Copy(BitConverter.GetBytes(ReplayRecorder.Instance.RecordedFrames[ReplayRecorder.Instance.RecordedFrames.Count - 1].time), 0, packed, transforms.Length + 1, 4);

			return Tuple.Create<byte[], bool>(packed, useKey);
		}

		private List<AudioOneShotEvent> lastSentOneShots = new List<AudioOneShotEvent>();
		private List<AudioClipEvent> lastSentClipEvents = new List<AudioClipEvent>();
		private List<AudioVolumeEvent> lastSentVolumeEvents = new List<AudioVolumeEvent>();
		private List<AudioPitchEvent> lastSentPitchEvents = new List<AudioPitchEvent>();
		private List<AudioCutoffEvent> lastSentCutoffEvents = new List<AudioCutoffEvent>();

		public byte[] PackSounds() {
			List<List<AudioOneShotEvent>> newOneShotEvents = new List<List<AudioOneShotEvent>>();
			List<List<AudioClipEvent>> newClipEvents = new List<List<AudioClipEvent>>();
			List<List<AudioVolumeEvent>> newVolumeEvents = new List<List<AudioVolumeEvent>>();
			List<List<AudioPitchEvent>> newPitchEvents = new List<List<AudioPitchEvent>>();
			List<List<AudioCutoffEvent>> newCutoffEvents = new List<List<AudioCutoffEvent>>();
			
			for (int i = 0; i < MultiplayerUtils.audioPlayerNames.Count; i ++) {
				newOneShotEvents.Add(new List<AudioOneShotEvent>());
				newClipEvents.Add(new List<AudioClipEvent>());
				newVolumeEvents.Add(new List<AudioVolumeEvent>());
				newPitchEvents.Add(new List<AudioPitchEvent>());
				newCutoffEvents.Add(new List<AudioCutoffEvent>());
				
				if (lastSentOneShots.Count < i + 1)
					lastSentOneShots.Add(null);
				if (lastSentClipEvents.Count < i + 1)
					lastSentClipEvents.Add(null);
				if (lastSentVolumeEvents.Count < i + 1)
					lastSentVolumeEvents.Add(null);
				if (lastSentPitchEvents.Count < i + 1)
					lastSentPitchEvents.Add(null);
				if (lastSentCutoffEvents.Count < i + 1)
					lastSentCutoffEvents.Add(null);

				// Out of range exception
				int previousOneShotIndex = lastSentOneShots[i] == null || !ReplayRecorder.Instance.oneShotEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) ? -1 : ReplayRecorder.Instance.oneShotEvents[MultiplayerUtils.audioPlayerNames[i]].IndexOf(lastSentOneShots[i]);
				int previousClipIndex = lastSentClipEvents[i] == null || !ReplayRecorder.Instance.clipEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) ? -1 : ReplayRecorder.Instance.clipEvents[MultiplayerUtils.audioPlayerNames[i]].IndexOf(lastSentClipEvents[i]);
				int previousVolumeIndex = lastSentVolumeEvents[i] == null || !ReplayRecorder.Instance.volumeEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) ? -1 : ReplayRecorder.Instance.volumeEvents[MultiplayerUtils.audioPlayerNames[i]].IndexOf(lastSentVolumeEvents[i]);
				int previousPitchIndex = lastSentPitchEvents[i] == null || !ReplayRecorder.Instance.pitchEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) ? -1 : ReplayRecorder.Instance.pitchEvents[MultiplayerUtils.audioPlayerNames[i]].IndexOf(lastSentPitchEvents[i]);
				int previousCutoffIndex = lastSentCutoffEvents[i] == null || !ReplayRecorder.Instance.cutoffEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i]) ? -1 : ReplayRecorder.Instance.cutoffEvents[MultiplayerUtils.audioPlayerNames[i]].IndexOf(lastSentCutoffEvents[i]);
				
				if (ReplayRecorder.Instance.oneShotEvents != null && ReplayRecorder.Instance.oneShotEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i])) {
					for (int j = previousOneShotIndex + 1; j < ReplayRecorder.Instance.oneShotEvents[MultiplayerUtils.audioPlayerNames[i]].Count; j++) {
						newOneShotEvents[i].Add(ReplayRecorder.Instance.oneShotEvents[MultiplayerUtils.audioPlayerNames[i]][j]);
					}
				}
				if (ReplayRecorder.Instance.clipEvents != null && ReplayRecorder.Instance.clipEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i])) {
					for (int j = previousClipIndex + 1; j < ReplayRecorder.Instance.clipEvents[MultiplayerUtils.audioPlayerNames[i]].Count; j++) {
						newClipEvents[i].Add(ReplayRecorder.Instance.clipEvents[MultiplayerUtils.audioPlayerNames[i]][j]);
					}
				}
				if (ReplayRecorder.Instance.volumeEvents != null && ReplayRecorder.Instance.volumeEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i])) {
					for (int j = previousVolumeIndex + 1; j < ReplayRecorder.Instance.volumeEvents[MultiplayerUtils.audioPlayerNames[i]].Count; j++) {
						newVolumeEvents[i].Add(ReplayRecorder.Instance.volumeEvents[MultiplayerUtils.audioPlayerNames[i]][j]);
					}
				}
				if (ReplayRecorder.Instance.pitchEvents != null && ReplayRecorder.Instance.pitchEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i])) {
					for (int j = previousPitchIndex + 1; j < ReplayRecorder.Instance.pitchEvents[MultiplayerUtils.audioPlayerNames[i]].Count; j++) {
						newPitchEvents[i].Add(ReplayRecorder.Instance.pitchEvents[MultiplayerUtils.audioPlayerNames[i]][j]);
					}
				}
				if (ReplayRecorder.Instance.cutoffEvents != null && ReplayRecorder.Instance.cutoffEvents.ContainsKey(MultiplayerUtils.audioPlayerNames[i])) {
					for (int j = previousCutoffIndex + 1; j < ReplayRecorder.Instance.cutoffEvents[MultiplayerUtils.audioPlayerNames[i]].Count; j++) {
						newCutoffEvents[i].Add(ReplayRecorder.Instance.cutoffEvents[MultiplayerUtils.audioPlayerNames[i]][j]);
					}
				}
			}
			
			List<List<byte>> newOneShotBytes = new List<List<byte>>();
			List<List<byte>> newClipBytes = new List<List<byte>>();
			List<List<byte>> newVolumeBytes = new List<List<byte>>();
			List<List<byte>> newPitchBytes = new List<List<byte>>();
			List<List<byte>> newCutoffBytes = new List<List<byte>>();

			List<byte> newAudioBytes = new List<byte>();
			
			for (int i = 0; i < MultiplayerUtils.audioPlayerNames.Count; i++) {
				newOneShotBytes.Add(new List<byte>());
				newClipBytes.Add(new List<byte>());
				newVolumeBytes.Add(new List<byte>());
				newPitchBytes.Add(new List<byte>());
				newCutoffBytes.Add(new List<byte>());

				lastSentOneShots[i] = newOneShotEvents[i].Count > 0 ? newOneShotEvents[i][newOneShotEvents[i].Count - 1] : lastSentOneShots[i];
				lastSentClipEvents[i] = newClipEvents[i].Count > 0 ? newClipEvents[i][newClipEvents[i].Count - 1] : lastSentClipEvents[i];
				lastSentVolumeEvents[i] = newVolumeEvents[i].Count > 0 ? newVolumeEvents[i][newVolumeEvents[i].Count - 1] : lastSentVolumeEvents[i];
				lastSentPitchEvents[i] = newPitchEvents[i].Count > 0 ? newPitchEvents[i][newPitchEvents[i].Count - 1] : lastSentPitchEvents[i];
				lastSentCutoffEvents[i] = newCutoffEvents[i].Count > 0 ? newCutoffEvents[i][newCutoffEvents[i].Count - 1] : lastSentCutoffEvents[i];
				
				// 21 audio event players
				// 2 floats, 1 string
				// 1 float, 1 string, 1 bool
				// 3x 2 floats

				for (int j = 0; j < newOneShotEvents[i].Count; j++) {
					ushort clipNameIndex = (ushort)MultiplayerUtils.audioClipNames.FindIndex(c => c.Equals(newOneShotEvents[i][j].clipName));
					float time = newOneShotEvents[i][j].time;
					float volume = newOneShotEvents[i][j].volumeScale;
					
					newOneShotBytes[i].AddRange(BitConverter.GetBytes(clipNameIndex));
					newOneShotBytes[i].AddRange(BitConverter.GetBytes(time));
					newOneShotBytes[i].AddRange(BitConverter.GetBytes(volume));
				}
				for (int j = 0; j < newClipEvents[i].Count; j++) {
					ushort clipNameIndex = newClipEvents[i][j].clipName == null ? ushort.MaxValue : (ushort)MultiplayerUtils.audioClipNames.FindIndex(c => c.Equals(newClipEvents[i][j].clipName));
					float time = newClipEvents[i][j].time;
					byte playing = newClipEvents[i][j].isPlaying ? (byte)1 : (byte)0;

					newClipBytes[i].AddRange(BitConverter.GetBytes(clipNameIndex));
					newClipBytes[i].AddRange(BitConverter.GetBytes(time));
					newClipBytes[i].Add(playing);
				}
				for (int j = 0; j < newVolumeEvents[i].Count; j++) {
					float time = newVolumeEvents[i][j].time;
					float volume = newVolumeEvents[i][j].volume;

					newVolumeBytes[i].AddRange(BitConverter.GetBytes(time));
					newVolumeBytes[i].AddRange(BitConverter.GetBytes(volume));
				}
				for (int j = 0; j < newPitchEvents[i].Count; j++) {
					float time = newPitchEvents[i][j].time;
					float pitch = newPitchEvents[i][j].pitch;

					newPitchBytes[i].AddRange(BitConverter.GetBytes(time));
					newPitchBytes[i].AddRange(BitConverter.GetBytes(pitch));
				}
				for (int j = 0; j < newCutoffEvents[i].Count; j++) {
					float time = newCutoffEvents[i][j].time;
					float cutoff = newCutoffEvents[i][j].cutoff;

					newCutoffBytes[i].AddRange(BitConverter.GetBytes(time));
					newCutoffBytes[i].AddRange(BitConverter.GetBytes(cutoff));
				}
				
				newAudioBytes.AddRange(BitConverter.GetBytes(newOneShotEvents[i].Count));
				newAudioBytes.AddRange(newOneShotBytes[i]);

				newAudioBytes.AddRange(BitConverter.GetBytes(newClipEvents[i].Count));
				newAudioBytes.AddRange(newClipBytes[i]);

				newAudioBytes.AddRange(BitConverter.GetBytes(newVolumeEvents[i].Count));
				newAudioBytes.AddRange(newVolumeBytes[i]);

				newAudioBytes.AddRange(BitConverter.GetBytes(newPitchEvents[i].Count));
				newAudioBytes.AddRange(newPitchBytes[i]);

				newAudioBytes.AddRange(BitConverter.GetBytes(newCutoffEvents[i].Count));
				newAudioBytes.AddRange(newCutoffBytes[i]);
			}

			byte[] compressedAudio = MultiplayerController.Compress(newAudioBytes.ToArray());

			byte[] sendPacket = new byte[compressedAudio.Length + 1];
			sendPacket[0] = (byte)OpCode.Sound;
			Array.Copy(compressedAudio, 0, sendPacket, 1, compressedAudio.Length);

			return sendPacket;
		}
	}
}
