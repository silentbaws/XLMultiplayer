using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityModManagerNet;
using RootMotion.FinalIK;
using Harmony12;
using System.Linq;
using ReplayEditor;

//TODO: ITS ALL SPAGHETTI

namespace XLMultiplayer {
	public enum MPTextureType : byte {
		Shirt = 0,
		Pants = 1,
		Shoes = 2, 
		Hat = 3,
		Board = 4
	}

	public class MultiplayerTexture {
		public byte[] bytes = null;
		public MPTextureType textureType;
		public Vector2 size;

		bool useFull = false;

		Texture2D texture;

		StreamWriter debugWriter;

		bool useTexture = true;

		string file;
		public bool loaded = false;
		public bool saved = false;

		public MultiplayerTexture(byte[] b, Vector2 s, MPTextureType t, StreamWriter sw) {
			bytes = b;
			size = s;
			textureType = t;
			string path = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp";
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			File.WriteAllBytes(path + "\\" + t.ToString() + ".jpg", b);
			saved = true;
		}

		public MultiplayerTexture(StreamWriter sw, MPTextureType t) {
			this.debugWriter = sw;
			textureType = t;
		}

		public void LoadFromFileMainThread(MultiplayerPlayerController controller) {
			if (useTexture) {
				debugWriter.WriteLine("LOADING TEXTURE FROM MAIN THREAD");
				byte[] data = File.ReadAllBytes(file);
				texture = new Texture2D((int)size.x, (int)size.y, TextureFormat.RGBA32, false);
				texture.LoadImage(data);
				controller.SetPlayerTexture(texture, textureType, useFull);
				loaded = true;
			}else if(textureType == MPTextureType.Shirt) {
				controller.SetPlayerTexture(null, MPTextureType.Shirt, useFull);
			}
		}

		public void SaveTexture(int connectionId, byte[] buffer) {
			debugWriter.WriteLine("Saving texture in queue");
			size = new Vector2(BitConverter.ToSingle(buffer, 3), BitConverter.ToSingle(buffer, 7));
			useFull = buffer[11] == 1 ? true : false;
			byte[] file = new byte[buffer.Length - 12];
			Array.Copy(buffer, 12, file, 0, file.Length);

			if(file.Length == 1) {
				useTexture = false;
			} else {
				if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing"))
					Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing");

				File.WriteAllBytes(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + connectionId.ToString() + ".jpg", file);

				this.file = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + connectionId.ToString() + ".jpg";
				saved = true;
				debugWriter.WriteLine("Saved texture in queue");
			}
		}
	}

	public class MultiplayerFrameBufferObject {
		public Vector3[] vectors = null;
		public Quaternion[] quaternions = null;

		public int animFrame = 0;

		public bool key = false;

		public float frameTime;
		public float deltaTime = 0;
		public float timeSinceStart = 0;

		public MultiplayerFrameBufferObject() {
			animFrame = 0;
			deltaTime = 0;
			timeSinceStart = 0;
		}
	}

	public class MultiplayerPositionFrameBufferObject {
		public Vector3[] vectors;
		public Quaternion[] quaternions;

		public int positionFrame = 0;
	}

	public class MultiplayerPlayerController {
		public GameObject player { get; private set; }
		public GameObject skater { get; private set; }
		public GameObject skaterMeshesObject { get; private set; }
		public GameObject board { get; private set; }

		public Transform hips;
		private Transform[] bones;

		public GameObject hatObject, shirtObject, pantsObject, shoesObject, headArmsObject;
		
		public Vector3[] targetPositions = new Vector3[68];
		public Quaternion[] targetRotations = new Quaternion[68];

		public string username = "IT ALL BROKE";

		public GameObject usernameObject;
		private TextMesh usernameText;

		private StreamWriter debugWriter;

		public byte playerID;

		private int currentAnimationPacket = -1;

		List<MultiplayerPositionFrameBufferObject> positionFrames = new List<MultiplayerPositionFrameBufferObject>();

		List<MultiplayerFrameBufferObject> animationFrames = new List<MultiplayerFrameBufferObject>();
		bool startedAnimating = false;
		float previousFrameTime = 0;

		private bool waitingForDelay = false;
		private bool speedDelay = false;

		public ReplayPlaybackController replayController;

		public List<ReplayRecordedFrame> recordedFrames = new List<ReplayRecordedFrame>();

		public CharacterCustomizer characterCustomizer {
			get {
				if(_characterCustomizer == null) {
					_characterCustomizer = this.player.GetComponentInChildren<CharacterCustomizer>();
				}
				return _characterCustomizer;
			}
		}

		public List<Tuple<CharacterGear, GameObject>> gearList {
			get {
				return Traverse.Create(characterCustomizer).Field("equippedGear").GetValue() as List<Tuple<CharacterGear, GameObject>>;
			}
		}

		private CharacterCustomizer _characterCustomizer;
		
		readonly string[] SkateboardMaterials = new string[] { "GripTape", "Deck", "Hanger", "Wheel1 Mesh", "Wheel2 Mesh", "Wheel3 Mesh", "Wheel4 Mesh" };
		
		public const string MainTextureName = "Texture2D_4128E5C7";
		public const string MainDeckTextureName = "Texture2D_694A07B4";

		Texture tShirtTexture;
		Texture pantsTexture;
		Texture shoesTexture;
		Texture hatTexture;
		Texture skateboardTexture;

		public MultiplayerTexture shirtMP;
		public MultiplayerTexture pantsMP;
		public MultiplayerTexture shoesMP;
		public MultiplayerTexture hatMP;
		public MultiplayerTexture boardMP;

		public bool copiedTextures = false;
		public bool startedEncoding = false;

		public bool loadedAll = false;

		public System.Collections.IEnumerator EncodeTextures() {
			if (!startedEncoding) {
				startedEncoding = true;
				Main.statusMenu.isLoading = true;
				Main.statusMenu.loadingStatus = 0;
				yield return new WaitForEndOfFrame();
				shirtMP = new MultiplayerTexture(ConvertTexture(tShirtTexture, MPTextureType.Shirt), new Vector2(tShirtTexture.width, tShirtTexture.height), MPTextureType.Shirt, debugWriter);
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();
				pantsMP = new MultiplayerTexture(ConvertTexture(pantsTexture, MPTextureType.Pants), new Vector2(pantsTexture.width, pantsTexture.height), MPTextureType.Pants, debugWriter);
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();
				shoesMP = new MultiplayerTexture(ConvertTexture(shoesTexture, MPTextureType.Shoes), new Vector2(shoesTexture.width, shoesTexture.height), MPTextureType.Shoes, debugWriter);
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();
				hatMP = new MultiplayerTexture(ConvertTexture(hatTexture, MPTextureType.Hat), new Vector2(hatTexture.width, hatTexture.height), MPTextureType.Hat, debugWriter);
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();
				boardMP = new MultiplayerTexture(ConvertTexture(skateboardTexture, MPTextureType.Board), new Vector2(skateboardTexture.width, skateboardTexture.height), MPTextureType.Board, debugWriter);
				copiedTextures = true;
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();

				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				Main.menu.multiplayerManager.SendTextures();
				yield return new WaitForEndOfFrame();

				Main.menu.multiplayerManager.InvokeRepeating("SendUpdate", 0.5f, 1.0f / (float)MultiplayerController.tickRate);
				yield return new WaitForEndOfFrame();
				Main.statusMenu.isLoading = false;
			}
			yield break;
		}

		private byte[] ConvertTexture(Texture t, MPTextureType texType) {
			Texture2D texture2D = null;
			if (t.width <= 4096 && t.height <= 4096) {
				texture2D = new Texture2D(t.width, t.height, TextureFormat.RGB24, false);

				RenderTexture currentRT = RenderTexture.active;

				RenderTexture renderTexture = new RenderTexture(t.width, t.height, 32);
				Graphics.Blit(t, renderTexture);

				RenderTexture.active = renderTexture;
				texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
				texture2D.Apply();
				
				if (texture2D.width > 1024 || texture2D.height > 1024)
					TextureScale.Bilinear(texture2D, 1024, 1024);

				RenderTexture.active = currentRT;
			}

			return texture2D == null ? new byte[1] { 0 } : texture2D.EncodeToJPG(80);
		}

		public void SetPlayerTexture(Texture tex, MPTextureType texType, bool useFull) {
			switch (texType) {
				case MPTextureType.Pants:
					foreach (Tuple<CharacterGear, GameObject> gearItem in gearList) {
						if (gearItem.Item1.categoryName.Equals("Pants")) {
							gearItem.Item2.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
							break;
						}
					}
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
						foreach (Tuple<CharacterGear, GameObject> gearItem in gearList) {
							if (gearItem.Item1.categoryName.Equals("Hoodie") || gearItem.Item1.categoryName.Equals("Shirt")) {
								gearItem.Item2.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
							}
						}
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
					foreach (Tuple<CharacterGear, GameObject> gearItem in gearList) {
						if (gearItem.Item1.categoryName.Equals("Hat")) {
							gearItem.Item2.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
							break;
						}
					}
					break;
				case MPTextureType.Board:
					foreach(Transform t in board.GetComponentsInChildren<Transform>()) {
						if (SkateboardMaterials.Contains(t.name)) {
							Renderer r = t.GetComponent<Renderer>();
							if(r != null) {
								r.material.SetTexture(MainDeckTextureName, tex);
							}
						}
					}
					break;
			}
		}

		public MultiplayerPlayerController(StreamWriter writer) {
			this.debugWriter = writer;
		}

		public static GameObject CustomLoadSMRPrefab(GameObject prefab, Transform root, Dictionary<string, Transform> bonesDict) {
			GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(prefab, root);
			foreach (GearPrefabController gearPrefabController in gameObject.GetComponentsInChildren<GearPrefabController>()) {
				try {
					gearPrefabController.SetBonesFromDict(bonesDict);
				} catch (Exception ex) {
				}
			}
			return gameObject;
		}

		public void ConstructForPlayer() {
			this.debugWriter.WriteLine("Constructing for Player");
			//Write the master prefab hierarchy to make sure everything is in place
			StreamWriter writer = new StreamWriter("Hierarchy.txt");
			writer.AutoFlush = true;
			foreach (Transform t in GameObject.Find("New Master Prefab(Clone)").GetComponentsInChildren<Transform>()) {
				Transform parent = t.parent;
				while (parent != null) {
					writer.Write("\t");
					parent = parent.parent;
				}
				writer.WriteLine("└─>" + t.name + (t.GetComponents<Rigidbody>().Length != 0 ? "<Contains rigidbody>" : ""));
			}

			writer.WriteLine("\nBEGIN RECORDED TRANSFORMS\n");

			foreach (Transform t in ReplayEditor.ReplayRecorder.Instance.RecordedTransforms) {
				Transform parent = t.parent;
				while (parent != null) {
					writer.Write("\t");
					parent = parent.parent;
				}
				writer.WriteLine("└─>" + t.name);
			}

			writer.WriteLine("\nBEGIN PLAYBACK TRANSFORMS\n");

			foreach (Transform t in ReplayEditorController.Instance.playbackController.playbackTransforms) {
				Transform parent = t.parent;
				while (parent != null) {
					writer.Write("\t");
					parent = parent.parent;
				}
				writer.WriteLine("└─>" + t.name);
			}

			writer.Close();
			this.debugWriter.WriteLine("Finished writing to hierarchy.txt");

			//Get the skater root gameobject and set it to the player
			this.player = PlayerController.Instance.skaterController.skaterTransform.gameObject;
			Transform[] componentsInChildren = PlayerController.Instance.gameObject.GetComponentsInChildren<Transform>();
			bool foundSkater = false;
			bool foundBoard = false;

			//Get the actual skater and skateboard from the root object
			for (int i = 0; i < componentsInChildren.Length; i++) {
				if (componentsInChildren[i].gameObject.name.Equals("NewSkater")) {
					if (!foundSkater) {
						if (componentsInChildren[i].Find("NewSteezeIK")) {
							this.skater = componentsInChildren[i].gameObject;
							this.debugWriter.WriteLine("Found Skater");
							foundSkater = true;
						}
					}
				} else if (componentsInChildren[i].gameObject.name.Equals("Skateboard")) {
					this.board = componentsInChildren[i].gameObject;
					this.debugWriter.WriteLine("Found Board");
					foreach(Transform t in componentsInChildren[i].GetComponentsInChildren<Transform>()) {
						if (t.name.Equals(SkateboardMaterials[0])) {
							skateboardTexture = t.GetComponent<Renderer>().material.GetTexture(MainDeckTextureName);
							foundBoard = true;
							break;
						}
					}
				}

				if(foundBoard && foundSkater) {
					break;
				}
			}

			if (!foundSkater) {
				this.debugWriter.WriteLine("Failed to find skater");
				return;
			}

			foreach (Transform child in skater.transform) {
				foreach (Transform t in child) {
					if (t.name.StartsWith("PAX", true, null)) {
						skaterMeshesObject = child.gameObject;
						break;
					}
				}
				if (skaterMeshesObject != null) break;
			}

			foreach (Tuple<CharacterGear, GameObject> t in gearList) {
				switch (t.Item1.categoryName) {
					case "Shirt":
						tShirtTexture = t.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName);
						break;
					case "Hoodie":
						tShirtTexture = t.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName);
						break;
					case "Hat":
						hatTexture = t.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName);
						break;
					case "Pants":
						pantsTexture = t.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName);
						break;
					case "Shoes":
						shoesTexture = t.Item2.transform.Find("Shoe_R").GetComponent<Renderer>().material.GetTexture(MainTextureName);
						break;
				}
			}

			this.hips = Traverse.Create(PlayerController.Instance.ikController).Field("_finalIk").GetValue<FullBodyBipedIK>().references.pelvis.parent;
		}

		public void ConstructFromPlayer(MultiplayerPlayerController source) {
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

			foreach (MonoBehaviour m in this.player.GetComponentsInChildren<MonoBehaviour>()) {
				if(m.GetType() == typeof(ReplayEditor.ReplayAudioEventPlayer)) {
					UnityEngine.Object.Destroy(m);
				}
			}

			this.player.SetActive(true);

			this.board = this.player.transform.Find("Skateboard").gameObject;
			this.board.transform.position = new Vector3(1111111, 111111111, 11111111);
			this.board.name = "New Player Board";

			this.skater = this.player.transform.Find("NewSkater").gameObject;
			this.skater.transform.position = new Vector3(1111111, 111111111, 11111111);
			this.skater.name = "New Player Skater";

			this.hips = this.skater.transform.Find("Skater_Joints").Find("Skater_root");

			this.skaterMeshesObject = this.skater.transform.Find("Skater").gameObject;

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

			characterCustomizer.ClothingParent = this.hips.Find("Skater_pelvis");
			characterCustomizer.RootBone = this.hips;
			Traverse.Create(characterCustomizer).Field("_bonesDict").SetValue(this.hips.GetComponentsInChildren<Transform>().ToDictionary((Transform t) => t.name));

			characterCustomizer.LoadCustomizations(PlayerController.Instance.characterCustomizer.CurrentCustomizations);

			debugWriter.WriteLine("Added gear back");

			bool foundHat = false, foundShirt = false, foundPants = false, foundShoes = false;

			foreach(Tuple<CharacterGear, GameObject> GearItem in gearList) {
				switch (GearItem.Item1.categoryName) {
					case "Hat":
						hatObject = GearItem.Item2;
						foundHat = true;
						break;
					case "Hoodie":
						shirtObject = GearItem.Item2;
						foundShirt = true;
						break;
					case "Shirt":
						shirtObject = GearItem.Item2;
						foundShirt = true;
						break;
					case "Pants":
						pantsObject = GearItem.Item2;
						foundPants = true;
						break;
					case "Shoes":
						shoesObject = GearItem.Item2;
						foundShoes = true;
						break;
				}
			}

			if (!foundHat) {
				CharacterGear newHat = CreateGear(GearCategory.Hat, "Hat", "PAX_1");
				characterCustomizer.LoadGear(newHat);
			}
			if (!foundPants) {
				CharacterGear newPants = CreateGear(GearCategory.Pants, "Pants", "PAX_1");
				characterCustomizer.LoadGear(newPants);
			}
			if (!foundShirt) {
				CharacterGear newShirt = CreateGear(GearCategory.Shirt, "Shirt", "PAX_1");
				characterCustomizer.LoadGear(newShirt);
			}
			if (!foundShoes) {
				CharacterGear newShoes = CreateGear(GearCategory.Shoes, "Shoes", "PAX_1");
				characterCustomizer.LoadGear(newShoes);
			}
			
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

			this.shirtMP = new MultiplayerTexture(this.debugWriter, MPTextureType.Shirt);
			this.pantsMP = new MultiplayerTexture(this.debugWriter, MPTextureType.Pants);
			this.hatMP = new MultiplayerTexture(this.debugWriter, MPTextureType.Hat);
			this.shoesMP = new MultiplayerTexture(this.debugWriter, MPTextureType.Shoes);
			this.boardMP = new MultiplayerTexture(this.debugWriter, MPTextureType.Board);
		}

		private CharacterGear CreateGear(GearCategory category, string categoryName, string id) {
			CharacterGear newGear = new CharacterGear();
			newGear.category = category;
			newGear.categoryName = categoryName;
			newGear.id = id;
			newGear.path = "CharacterCustomization/" + categoryName + "/" + id;
			return newGear;
		}

		public byte[] PackTransformInfoArray(List<ReplayRecordedFrame> frames, int start, bool useKey) {
			TransformInfo[] T = frames[ReplayRecorder.Instance.RecordedFrames.Count - 1].transformInfos;
			TransformInfo[] TPrevious = frames[ReplayRecorder.Instance.RecordedFrames.Count - 2].transformInfos;

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

				if(!useKey && GameManagement.GameStateMachine.Instance.CurrentState.GetType() == typeof(GameManagement.ReplayState)) {
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

		int framesSinceKey = 5;

		public byte[] PackAnimator() {
			framesSinceKey++;

			bool useKey = false;

			if (framesSinceKey >= 5) {
				framesSinceKey = 0;
				useKey = true;
			}

			byte[] transforms = PackTransformInfoArray(ReplayEditor.ReplayRecorder.Instance.RecordedFrames, 0, useKey);
			
			byte[] packed = new byte[transforms.Length + 5];
			packed[0] = useKey ? (byte)1 : (byte)0;
			Array.Copy(transforms, 0, packed, 1, transforms.Length);
			Array.Copy(BitConverter.GetBytes(ReplayEditor.ReplayRecorder.Instance.RecordedFrames[ReplayEditor.ReplayRecorder.Instance.RecordedFrames.Count-1].time), 0, packed, transforms.Length + 1, 4);
			
			return packed;
		}

		public void UnpackAnimator(byte[] recBuffer) {
			int receivedPacketSequence = BitConverter.ToInt32(recBuffer, 0);

			byte[] buffer = new byte[recBuffer.Length - 5];
			if (receivedPacketSequence < currentAnimationPacket - 5) {
				return;
			} else {
				Array.Copy(recBuffer, 5, buffer, 0, recBuffer.Length - 5);
				currentAnimationPacket = receivedPacketSequence > currentAnimationPacket ? receivedPacketSequence : currentAnimationPacket;
			}

			if (animationFrames.Find(f => f.animFrame == receivedPacketSequence) != null) {
				debugWriter.WriteLine("Duped frame of animation for player: " + this.username);
				return;
			}

			MultiplayerFrameBufferObject currentBufferObject = new MultiplayerFrameBufferObject();

			currentBufferObject.key = recBuffer[4] == (byte)1 ? true : false;
			currentBufferObject.animFrame = receivedPacketSequence;

			currentBufferObject.frameTime = BitConverter.ToSingle(buffer, buffer.Length - 4);

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
			
			this.animationFrames.Add(currentBufferObject);
			this.animationFrames = this.animationFrames.OrderBy(f => f.animFrame).ToList();
		}

		public void LerpNextFrame(bool inReplay, bool recursive = false, float offset = 0, int recursionLevel = 0) {
			if (this.animationFrames.Count == 0 || this.animationFrames[0] == null) return;
			if (!startedAnimating && animationFrames.Count > 5) {
				if (this.animationFrames[0].vectors == null || !this.animationFrames[0].key) {
					this.animationFrames.RemoveAt(0);
					LerpNextFrame(inReplay);
				}

				if(this.animationFrames.Count > 6)
					this.animationFrames.RemoveRange(0, (this.animationFrames.Count - 6));

				startedAnimating = true;

				for (int i = 0; i < 77; i++) {
					bones[i].localPosition = this.animationFrames[0].vectors[i];
					bones[i].localRotation = this.animationFrames[0].quaternions[i];
				}

				this.previousFrameTime = this.animationFrames[0].frameTime;

				this.animationFrames.RemoveAt(0);
			}
			if (!startedAnimating) return;

			int frameDelay = 0;

			if (this.animationFrames.Count < 2) return;

			if(this.animationFrames.Count < 4 || this.waitingForDelay) {
				this.waitingForDelay = !(this.animationFrames.Count > 5);
			}

			if (this.animationFrames.Count >= 10 || this.speedDelay) {
				if(this.animationFrames.Count > 50) {
					this.animationFrames.RemoveRange(0, this.animationFrames.Count - 20);
				}

				this.speedDelay = !(this.animationFrames.Count < 7);
			}

			if(this.waitingForDelay || this.speedDelay) {
				frameDelay = this.animationFrames.Count - 6;
			}

			if (this.animationFrames[0].vectors == null) {
				this.animationFrames.RemoveAt(0);

				LerpNextFrame(inReplay);
			}

			if (this.animationFrames[0].deltaTime == 0) {
				this.animationFrames[0].deltaTime = this.animationFrames[0].frameTime - this.previousFrameTime;

				if(this.animationFrames[0].deltaTime == 0.0f) {
					this.animationFrames[0].deltaTime = 1f / 30f;
				}

				if(frameDelay != 0) {
					debugWriter.Write("Adjusting current animation frame time from: " + this.animationFrames[0].deltaTime);
					this.animationFrames[0].deltaTime = frameDelay < 0 ? this.animationFrames[0].deltaTime * Mathf.Max(Mathf.Abs(frameDelay), 2) : this.animationFrames[0].deltaTime / Mathf.Min(Mathf.Max(frameDelay, 2), 6);
					debugWriter.WriteLine("  To: " + this.animationFrames[0].deltaTime);

					if(this.animationFrames[0].deltaTime > 0.14f) {
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
				//30FPS 120Seconds
				if(this.recordedFrames.Count > 30 * 120) {
					this.recordedFrames.RemoveAt(0);
					this.recordedFrames.Add(new ReplayRecordedFrame(BufferToInfo(this.animationFrames[0]), Time.time));
				} else {
					this.recordedFrames.Add(new ReplayRecordedFrame(BufferToInfo(this.animationFrames[0]), Time.time));
				}

				if (!inReplay) {
					for (int i = 0; i < 77; i++) {
						bones[i].localPosition = this.animationFrames[0].vectors[i];
						bones[i].localRotation = this.animationFrames[0].quaternions[i];
					}
				}

				if (!this.animationFrames[1].key) {
					for(int i = 0; i < 77; i++) {
						this.animationFrames[1].vectors[i] = this.animationFrames[0].vectors[i] + this.animationFrames[1].vectors[i];
						this.animationFrames[1].quaternions[i].eulerAngles = this.animationFrames[0].quaternions[i].eulerAngles + this.animationFrames[1].quaternions[i].eulerAngles;
					}
				}

				float oldTime = this.animationFrames[0].timeSinceStart;
				float oldDelta = this.animationFrames[0].deltaTime;

				this.previousFrameTime = this.animationFrames[0].frameTime;
				this.animationFrames.RemoveAt(0);
				if(recursionLevel < 4) {
					this.LerpNextFrame(inReplay, true, oldTime - oldDelta, recursionLevel+1);
				}
			}
		}

		TransformInfo[] BufferToInfo(MultiplayerFrameBufferObject frame) {
			TransformInfo[] info = new TransformInfo[replayController.playbackTransforms.Count];

			for(int i = 0; i < info.Length; i++) {
				info[i] = new TransformInfo(this.skater.transform);
				info[i].position = frame.vectors[i];
				info[i].rotation = frame.quaternions[i];
			}

			return info;
		}

		public void EnsureQuaternionListContinuity() {
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