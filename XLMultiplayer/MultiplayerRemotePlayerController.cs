using Harmony12;
using ReplayEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

	public class MultiplayerRemotePlayerController : MultiplayerPlayerController {
		public GameObject skater { get; private set; }
		public GameObject skaterMeshObjects { get; private set; }
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

		List<MultiplayerFrameBufferObject> animationFrames = new List<MultiplayerFrameBufferObject>();
		List<MultiplayerFrameBufferObject> replayAnimationFrames = new List<MultiplayerFrameBufferObject>();
		public List<ReplayRecordedFrame> recordedFrames = new List<ReplayRecordedFrame>();

		public MultiplayerRemoteTexture shirtMPTex;
		public MultiplayerRemoteTexture pantsMPTex;
		public MultiplayerRemoteTexture shoesMPTex;
		public MultiplayerRemoteTexture hatMPTex;
		public MultiplayerRemoteTexture boardMPTex;

		public ReplayPlaybackController replayController;

		public byte playerID = 255;

		public MultiplayerRemotePlayerController(StreamWriter writer) : base(writer) {  }

		//TODO: half this stuff probably isn't needed
		override public void ConstructPlayer() {
			//Create a new root object for the player
			this.player = GameObject.Instantiate<GameObject>(ReplayEditor.ReplayEditorController.Instance.playbackController.gameObject);
			UnityEngine.Object.DontDestroyOnLoad(this.player);
			this.player.name = "New Player";
			this.player.transform.SetParent(null);
			this.player.transform.position = PlayerController.Instance.transform.position;
			this.player.transform.rotation = PlayerController.Instance.transform.rotation;
			debugWriter.WriteLine("Created New Player");

			this.replayController = this.player.GetComponentInChildren<ReplayPlaybackController>();

			this.bones = replayController.playbackTransforms.ToArray();

			//UnityEngine.Object.DestroyImmediate(this.player.GetComponentInChildren<ReplayEditor.ReplayPlaybackController>());

			//foreach (MonoBehaviour m in this.player.GetComponentsInChildren<MonoBehaviour>()) {
			//	if (m.GetType() == typeof(ReplayEditor.ReplayAudioEventPlayer)) {
			//		UnityEngine.Object.Destroy(m);
			//	}
			//}

			this.player.SetActive(true);

			this.board = this.player.transform.Find("Skateboard").gameObject;
			this.board.transform.position = new Vector3(0, 0, 0);
			this.board.name = "New Player Board";

			this.skater = this.player.transform.Find("NewSkater").gameObject;
			this.skater.transform.position = new Vector3(0, 0, 0);
			this.skater.name = "New Player Skater";

			Transform hips = this.skater.transform.Find("Skater_Joints").Find("Skater_root");
			
			GameObject skaterMeshesObject = this.skater.transform.Find("Skater").gameObject;

			debugWriter.WriteLine("created everything");

			characterCustomizer.enabled = true;
			characterCustomizer.RemoveAllGear();
			foreach (Transform t in skaterMeshesObject.GetComponentsInChildren<Transform>()) {
				if (t.gameObject != null)
					GameObject.Destroy(t.gameObject);
			}

			debugWriter.WriteLine("Removed gear");

			debugWriter.WriteLine(characterCustomizer.ClothingParent.name);
			debugWriter.WriteLine(characterCustomizer.RootBone.name);

			this.characterCustomizer.ClothingParent = hips.Find("Skater_pelvis");
			this.characterCustomizer.RootBone = hips;
			Traverse.Create(characterCustomizer).Field("_bonesDict").SetValue(bones.ToDictionary((Transform t) => t.name));

			characterCustomizer.LoadCustomizations(PlayerController.Instance.characterCustomizer.CurrentCustomizations);

			debugWriter.WriteLine("Added gear back");

			//bool foundHat = false, foundShirt = false, foundPants = false, foundShoes = false;

			//foreach(Tuple<CharacterGear, GameObject> GearItem in gearList) {
			//	switch (GearItem.Item1.categoryName) {
			//		case "Hat":
			//			foundHat = true;
			//			break;
			//		case "Hoodie":
			//			foundShirt = true;
			//			break;
			//		case "Shirt":
			//			foundShirt = true;
			//			break;
			//		case "Pants":
			//			foundPants = true;
			//			break;
			//		case "Shoes":
			//			foundShoes = true;
			//			break;
			//	}
			//}

			//if (!foundHat) {
			//	CharacterGear newHat = CreateGear(GearCategory.Hat, "Hat", "PAX_1");
			//	characterCustomizer.LoadGear(newHat);
			//}
			//if (!foundPants) {
			//	CharacterGear newPants = CreateGear(GearCategory.Pants, "Pants", "PAX_1");
			//	characterCustomizer.LoadGear(newPants);
			//}
			//if (!foundShirt) {
			//	CharacterGear newShirt = CreateGear(GearCategory.Shirt, "Shirt", "PAX_1");
			//	characterCustomizer.LoadGear(newShirt);
			//}
			//if (!foundShoes) {
			//	CharacterGear newShoes = CreateGear(GearCategory.Shoes, "Shoes", "PAX_1");
			//	characterCustomizer.LoadGear(newShoes);
			//}

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

			this.shirtMPTex = new MultiplayerRemoteTexture(MPTextureType.Shirt, this.debugWriter);
			this.pantsMPTex = new MultiplayerRemoteTexture(MPTextureType.Pants, this.debugWriter);
			this.hatMPTex = new MultiplayerRemoteTexture(MPTextureType.Hat, this.debugWriter);
			this.shoesMPTex = new MultiplayerRemoteTexture(MPTextureType.Shoes, this.debugWriter);
			this.boardMPTex = new MultiplayerRemoteTexture(MPTextureType.Board, this.debugWriter);
		}

		// Create gear for players
		private CharacterGear CreateGear(GearCategory category, string categoryName, string id) {
			CharacterGear newGear = new CharacterGear();
			newGear.category = category;
			newGear.categoryName = categoryName;
			newGear.id = id;
			newGear.path = "CharacterCustomization/" + categoryName + "/" + id;
			return newGear;
		}

		private void SetClothingTexture(string[] names, Texture tex) {
			foreach (Tuple<CharacterGear, GameObject> gearItem in gearList) {
				if (names.Contains(gearItem.Item1.categoryName)) {
					gearItem.Item2.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
					break;
				}
			}
		}

		public void SetPlayerTexture(Texture tex, MPTextureType texType, bool useFull) {
			switch (texType) {
				case MPTextureType.Pants:
					SetClothingTexture(new string[] { "Pants" }, tex);
					break;
				case MPTextureType.Shirt:
					foreach (Tuple<CharacterGear, GameObject> gearItem in gearList) {
						if (gearItem.Item1.categoryName.Equals("Hoodie") || gearItem.Item1.categoryName.Equals("Shirt")) {
							CharacterGear newGear = new CharacterGear();
							newGear.id = "PAX_1";
							newGear.name = "Black";
							newGear.category = useFull ? GearCategory.Hoodie : GearCategory.Shirt;
							newGear.categoryName = useFull ? "Hoodie" : "Shirt";
							newGear.path = useFull ? "CharacterCustomization/Hoodie/PAX_1" : "CharacterCustomization/Shirt/PAX_1";

							characterCustomizer.LoadGear(newGear);
							break;
						}
					}
					if (tex != null) {
						SetClothingTexture(new string[] { "Hoodie", "Shirt" }, tex);
					}
					break;
				case MPTextureType.Shoes:
					foreach (Tuple<CharacterGear, GameObject> gearItem in gearList) {
						if (gearItem.Item1.categoryName.Equals("Shoes")) {
							GameObject Shoe_L = gearItem.Item2.transform.Find("Shoe_L").gameObject;
							GameObject Shoe_R = gearItem.Item2.transform.Find("Shoe_R").gameObject;

							Shoe_L.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
							Shoe_R.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
							break;
						}
					}
					break;
				case MPTextureType.Hat:
					SetClothingTexture(new string[] { "Hat" }, tex);
					break;
				case MPTextureType.Board:
					foreach (Transform t in board.GetComponentsInChildren<Transform>()) {
						if (SkateboardMaterials.Contains(t.name)) {
							Renderer r = t.GetComponent<Renderer>();
							if (r != null) {
								r.material.SetTexture(MainDeckTextureName, tex);
							}
						}
					}
					break;
			}
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
				this.startAnimTime = Time.time;
			}

			if (this.replayAnimationFrames.Count > 30 * 120) {
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
			this.replayAnimationFrames = this.replayAnimationFrames.OrderBy(f => f.animFrame).ToList();
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
				this.replayAnimationFrames.Find(f => f.animFrame == this.animationFrames[0].animFrame).realFrameTime = Time.time;

				if (!inReplay) {
					for (int i = 0; i < 77; i++) {
						bones[i].localPosition = this.animationFrames[0].vectors[i];
						bones[i].localRotation = this.animationFrames[0].quaternions[i];
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
					MultiplayerFrameBufferObject firstFrameWithTime = this.replayAnimationFrames.First(obj => obj.realFrameTime != -1f);
					frame.realFrameTime = firstFrameWithTime.realFrameTime + (frame.animFrame - firstFrameWithTime.animFrame) * averageFrameTime;
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
				}
				if (frame.key) {
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

			this.replayController.enabled = true;
			this.recordedFrames = this.recordedFrames.OrderBy(f => f.time).ToList();
			this.EnsureQuaternionListContinuity();
			Traverse.Create(this.replayController).Property("ClipFrames").SetValue((from f in this.recordedFrames select f.Copy()).ToList());
			Traverse.Create(this.replayController).Field("m_audioEventPlayers").SetValue(new List<ReplayAudioEventPlayer>());
			{ // Subtract Start Time
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
	}
}
