using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityModManagerNet;
using RootMotion.FinalIK;
using Harmony12;
using System.Linq;

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
			File.WriteAllBytes(path + "\\" + t.ToString() + ".png", b);
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

				File.WriteAllBytes(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + connectionId.ToString() + ".png", file);

				this.file = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + connectionId.ToString() + ".png";
				saved = true;
				debugWriter.WriteLine("Saved texture in queue");
			}
		}
	}

	public class MultiplayerFrameBufferObject {
		public Vector3[] topHalfVectors = null;
		public Quaternion[] topHalfQuaternions = null;
		public Vector3[] bottomHalfVectors = null;
		public Quaternion[] bottomHalfQuaternions = null;

		public int animFrame = 0;

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

		public GameObject hatObject, shirtObject, pantsObject, shoesObject, headArmsObject;
		
		public Vector3[] targetPositions = new Vector3[68];
		public Quaternion[] targetRotations = new Quaternion[68];

		public string username = "IT ALL BROKE";

		private GameObject usernameObject;
		private TextMesh usernameText;

		private StreamWriter debugWriter;

		public byte playerID;

		private int currentAnimationPacket = -1;
		private int currentPositionPacket = -1;

		List<MultiplayerPositionFrameBufferObject> positionFrames = new List<MultiplayerPositionFrameBufferObject>();

		List<MultiplayerFrameBufferObject> animationFrames = new List<MultiplayerFrameBufferObject>();
		bool startedAnimating = false;
		float previousFrameTime = 0;

		private bool waitingForDelay = false;

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

			return texture2D == null ? new byte[1] { 0 } : texture2D.EncodeToPNG();
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
			//SET textures from player here

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
			debugWriter.WriteLine("Created New Player");

			UnityEngine.Object.DestroyImmediate(this.player.GetComponentInChildren<ReplayEditor.ReplayPlaybackController>());

			foreach (MonoBehaviour m in this.player.GetComponentsInChildren<MonoBehaviour>()) {
				if(m.GetType() == typeof(ReplayEditor.ReplayAudioEventPlayer)) {
					UnityEngine.Object.Destroy(m);
				}
			}

			this.player.SetActive(true);

			this.board = this.player.transform.Find("Skateboard").gameObject;
			this.board.name = "New Player Board";

			this.skater = this.player.transform.Find("NewSkater").gameObject;
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

		public byte[] PackTransforms() {
			Transform[] T = new Transform[] { this.player.transform, this.board.transform, this.skater.transform };
			
			byte[] TPacked = this.PackTransformArray(T);

			return TPacked;
		}

		int outoforder = 0;
		int total = 0;

		public void UnpackTransforms(byte[] recBuffer) {
			int receivedPacketSequence = BitConverter.ToInt32(recBuffer, 0);
			total++;
			byte[] buffer = new byte[recBuffer.Length - 4];
			if (receivedPacketSequence < currentPositionPacket - 5) {
				outoforder++;
				return;
			} else {
				currentPositionPacket = Math.Max(receivedPacketSequence, currentPositionPacket);
				Array.Copy(recBuffer, 4, buffer, 0, recBuffer.Length - 4);
			}

			List<Vector3> vectors = new List<Vector3>();
			List<Quaternion> quaternions = new List<Quaternion>();

			for (int i = 0; i < 3; i++) {
				Vector3 readVector = new Vector3();
				readVector.x = BitConverter.ToSingle(buffer, i * 28);
				readVector.y = BitConverter.ToSingle(buffer, i * 28 + 4);
				readVector.z = BitConverter.ToSingle(buffer, i * 28 + 8);
				Quaternion readQuaternion = new Quaternion();
				readQuaternion.x = BitConverter.ToSingle(buffer, i * 28 + 12);
				readQuaternion.y = BitConverter.ToSingle(buffer, i * 28 + 16);
				readQuaternion.z = BitConverter.ToSingle(buffer, i * 28 + 20);
				readQuaternion.w = BitConverter.ToSingle(buffer, i * 28 + 24);

				vectors.Add(readVector);
				quaternions.Add(readQuaternion);
			}

			SetTransforms(vectors.ToArray(), quaternions.ToArray(), receivedPacketSequence);
		}

		public void SetTransforms(Vector3[] vectors, Quaternion[] quaternions, int posFrame) {
			MultiplayerPositionFrameBufferObject newPositionObject = new MultiplayerPositionFrameBufferObject();
			newPositionObject.vectors = vectors;
			newPositionObject.quaternions = quaternions;
			newPositionObject.positionFrame = posFrame;

			positionFrames.Add(newPositionObject);

			this.positionFrames = this.positionFrames.OrderBy(f => f.positionFrame).ToList();
		}

		public byte[] PackTransformArray(Transform[] T) {
			byte[] packed = new byte[T.Length * 28];
			for (int i = 0; i < T.Length; i++) {
				Array.Copy(BitConverter.GetBytes(T[i].position.x), 0, packed, i * 28, 4);
				Array.Copy(BitConverter.GetBytes(T[i].position.y), 0, packed, i * 28 + 4, 4);
				Array.Copy(BitConverter.GetBytes(T[i].position.z), 0, packed, i * 28 + 8, 4);
				Array.Copy(BitConverter.GetBytes(T[i].rotation.x), 0, packed, i * 28 + 12, 4);
				Array.Copy(BitConverter.GetBytes(T[i].rotation.y), 0, packed, i * 28 + 16, 4);
				Array.Copy(BitConverter.GetBytes(T[i].rotation.z), 0, packed, i * 28 + 20, 4);
				Array.Copy(BitConverter.GetBytes(T[i].rotation.w), 0, packed, i * 28 + 24, 4);
			}
			return packed;
		}

		public byte[] PackTransformInfoArray(ReplayEditor.TransformInfo[] T, int start) {
			byte[] packed = new byte[T.Length * 14 - (start * 14)];

			for (int i = 0; i < T.Length - start; i++) {
				Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(T[i + start].position.x)), 0, packed, i * 14, 2);
				Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(T[i + start].position.y)), 0, packed, i * 14 + 2, 2);
				Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(T[i + start].position.z)), 0, packed, i * 14 + 4, 2);
				if (Quaternion.Angle(T[i + start].rotation, Quaternion.identity) < 0.01f) {
					for(int c = 6; c < 14; c++) {
						packed[i * 14 + c] = 0;
					}
				} else {
					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(T[i + start].rotation.x)), 0, packed, i * 14 + 6, 2);
					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(T[i + start].rotation.y)), 0, packed, i * 14 + 8, 2);
					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(T[i + start].rotation.z)), 0, packed, i * 14 + 10, 2);
					Array.Copy(SystemHalf.Half.GetBytes(SystemHalf.HalfHelper.SingleToHalf(T[i + start].rotation.w)), 0, packed, i * 14 + 12, 2);
				}
			}

			return packed;
		}

		System.Diagnostics.Stopwatch averageWatch = null;
		bool timing = false;
		double totalMS = 0;
		int ticks = 0;

		public byte[] PackAnimator() {
			if (!timing) {
				timing = true;
				averageWatch = System.Diagnostics.Stopwatch.StartNew();
			}
			var watch = System.Diagnostics.Stopwatch.StartNew();
			//byte[][] packed = new byte[2][];

			byte[] transforms = PackTransformInfoArray(ReplayEditor.ReplayRecorder.Instance.RecordedFrames[ReplayEditor.ReplayRecorder.Instance.RecordedFrames.Count - 1].transformInfos, 8);

			byte[] packed = new byte[956];
			//packed[0][0] = 0;
			Array.Copy(transforms, 0, packed, 0, 952);
			Array.Copy(BitConverter.GetBytes(ReplayEditor.ReplayRecorder.Instance.RecordedFrames[ReplayEditor.ReplayRecorder.Instance.RecordedFrames.Count-1].time), 0, packed, 952, 4);
			//packed[0][957] = 0;

			//packed[1] = new byte[954];
			//packed[1][0] = 1;
			//Array.Copy(transforms, 952, packed[1], 1, 952);
			//packed[1][953] = 1;
			watch.Stop();
			if (averageWatch.ElapsedMilliseconds >= 5000 && averageWatch.IsRunning) {
				double averageMS = totalMS / ticks;
				debugWriter.WriteLine("Average function time of " + averageMS.ToString() + " over " + ticks + " ticks");
				averageWatch.Stop();
			}
			totalMS += watch.Elapsed.TotalMilliseconds;
			ticks++;
			//debugWriter.WriteLine(watch.Elapsed.TotalMilliseconds);
			return packed;
		}

		public void UnpackAnimator(byte[] recBuffer) {
			int receivedPacketSequence = BitConverter.ToInt32(recBuffer, 0);

			byte[] buffer = new byte[recBuffer.Length - 4];
			if (receivedPacketSequence < currentAnimationPacket - 5) {
				return;
			} else {
				Array.Copy(recBuffer, 4, buffer, 0, recBuffer.Length - 4);
				currentAnimationPacket = receivedPacketSequence > currentAnimationPacket ? receivedPacketSequence : currentAnimationPacket;
			}

			MultiplayerFrameBufferObject currentBufferObject = new MultiplayerFrameBufferObject();

			currentBufferObject.animFrame = receivedPacketSequence;
			currentBufferObject.frameTime = BitConverter.ToSingle(buffer, buffer.Length - 4);

			List<Vector3> vectors = new List<Vector3>();
			List<Quaternion> quaternions = new List<Quaternion>();

			for (int i = 0; i < 68; i++) {
				Vector3 readVector = new Vector3();
				bool zeroVector = true;
				if(buffer[i * 14] == 0) {
					for(int c = 0; c < 6; c++) {
						if(buffer[i * 14 + c] != 0) {
							zeroVector = false;
							break;
						}
					}
				} else {
					zeroVector = false;
				}
				if (!zeroVector) {
					readVector.x = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 14));
					readVector.y = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 14 + 2));
					readVector.z = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 14 + 4));
				} else {
					readVector = Vector3.zero;
				}

				Quaternion readQuaternion = new Quaternion();
				bool zeroQuaternion = true;
				if (buffer[i * 14 + 6] == 0) {
					for (int c = 6; c < 14; c++) {
						if (buffer[i * 14 + c] != 0) {
							zeroQuaternion = false;
							break;
						}
					}
				} else {
					zeroQuaternion = false;
				}
				if (!zeroQuaternion) {
					readQuaternion.x = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 14 + 6));
					readQuaternion.y = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 14 + 8));
					readQuaternion.z = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 14 + 10));
					readQuaternion.w = SystemHalf.HalfHelper.HalfToSingle(SystemHalf.Half.ToHalf(buffer, i * 14 + 12));
				} else {
					readQuaternion = Quaternion.identity;
				}

				vectors.Add(readVector);
				quaternions.Add(readQuaternion);
			}
			
			currentBufferObject.topHalfVectors = new Vector3[34];
			currentBufferObject.topHalfQuaternions = new Quaternion[34];
			for (int i = 0; i < 34; i++) {
				currentBufferObject.topHalfVectors[i] = vectors[i];
				currentBufferObject.topHalfQuaternions[i] = quaternions[i];
			}

			currentBufferObject.bottomHalfVectors = new Vector3[34];
			currentBufferObject.bottomHalfQuaternions = new Quaternion[34];
			for (int i = 34; i < 68; i++) {
				currentBufferObject.bottomHalfVectors[i - 34] = vectors[i];
				currentBufferObject.bottomHalfQuaternions[i - 34] = quaternions[i];
			}
			
			this.animationFrames.Add(currentBufferObject);
			this.animationFrames = this.animationFrames.OrderBy(f => f.animFrame).ToList();
		}

		public void LerpNextFrame(bool recursive = false, float offset = 0) {
			if (this.animationFrames.Count == 0 || this.animationFrames[0] == null) return;
			if (!startedAnimating && animationFrames.Count > 5) {
				if (this.animationFrames[0].topHalfVectors == null || this.animationFrames[0].bottomHalfVectors == null) {
					this.animationFrames.RemoveAt(0);
					LerpNextFrame();
				}

				//debugWriter.WriteLine("Starting animation of other player with " + this.animationFrames.Count + " frame delay");

				while (this.animationFrames.Count > 6) {
					this.animationFrames.RemoveAt(0);
					if (this.animationFrames.Count == 6) break;
					//debugWriter.WriteLine("Removing frame currently still " + this.animationFrames.Count + " Frames behind");
				}

				startedAnimating = true;

				for (int i = 0; i < 68; i++) {
					if (i < 34) {
						this.hips.GetComponentsInChildren<Transform>()[i].localPosition = this.animationFrames[0].topHalfVectors[i];
						this.hips.GetComponentsInChildren<Transform>()[i].localRotation = this.animationFrames[0].topHalfQuaternions[i];
					} else {
						this.hips.GetComponentsInChildren<Transform>()[i].localPosition = this.animationFrames[0].bottomHalfVectors[i - 34];
						this.hips.GetComponentsInChildren<Transform>()[i].localRotation = this.animationFrames[0].bottomHalfQuaternions[i - 34];
					}
				}

				this.previousFrameTime = this.animationFrames[0].frameTime;

				this.animationFrames.RemoveAt(0);
			}
			if (!startedAnimating) return;

			if(this.animationFrames.Count < 3 || this.waitingForDelay) {
				this.waitingForDelay = !(this.animationFrames.Count > 5);
				//debugWriter.WriteLine("Waiting for 6 frame delay");
				return;
			}

			if (this.animationFrames.Count >= 10) {
				while (this.animationFrames.Count > 6) {
					this.animationFrames.RemoveAt(0);
					if (this.animationFrames[0].topHalfVectors != null) this.previousFrameTime = this.animationFrames[0].frameTime;
					if (this.animationFrames.Count == 6) break;
					//debugWriter.WriteLine("Removing frame currently still " + this.animationFrames.Count + " Frames behind");
				}
			}

			if (this.animationFrames[0].topHalfVectors == null || this.animationFrames[0].bottomHalfVectors == null) {
				this.animationFrames.RemoveAt(0);

				//debugWriter.WriteLine("Skipping frame");
				return;
			}

			if (this.animationFrames[0].deltaTime == 0) {
				this.animationFrames[0].deltaTime = this.animationFrames[0].frameTime - this.previousFrameTime;
			}

			if (!recursive) this.animationFrames[0].timeSinceStart += Time.unscaledDeltaTime;
			else this.animationFrames[0].timeSinceStart = offset;

			if (positionFrames.Count > 0 && positionFrames[0].positionFrame <= animationFrames[0].animFrame + 1)
				LerpPosition(positionFrames[0].vectors, positionFrames[0].quaternions, recursive ? offset : Time.unscaledDeltaTime);

			for (int i = 0; i < 68; i++) {
				if (i < 34) {
					this.hips.GetComponentsInChildren<Transform>()[i].localPosition = Vector3.Lerp(this.hips.GetComponentsInChildren<Transform>()[i].localPosition, this.animationFrames[0].topHalfVectors[i], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
					this.hips.GetComponentsInChildren<Transform>()[i].localRotation = Quaternion.Slerp(this.hips.GetComponentsInChildren<Transform>()[i].localRotation, this.animationFrames[0].topHalfQuaternions[i], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
				} else {
					this.hips.GetComponentsInChildren<Transform>()[i].localPosition = Vector3.Lerp(this.hips.GetComponentsInChildren<Transform>()[i].localPosition, this.animationFrames[0].bottomHalfVectors[i - 34], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
					this.hips.GetComponentsInChildren<Transform>()[i].localRotation = Quaternion.Slerp(this.hips.GetComponentsInChildren<Transform>()[i].localRotation, this.animationFrames[0].bottomHalfQuaternions[i - 34], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
				}
			}

			if (this.animationFrames[0].timeSinceStart >= this.animationFrames[0].deltaTime) {
				for (int i = 0; i < 68; i++) {
					if (i < 34) {
						this.hips.GetComponentsInChildren<Transform>()[i].localPosition = this.animationFrames[0].topHalfVectors[i];
						this.hips.GetComponentsInChildren<Transform>()[i].localRotation = this.animationFrames[0].topHalfQuaternions[i];
					} else {
						this.hips.GetComponentsInChildren<Transform>()[i].localPosition = this.animationFrames[0].bottomHalfVectors[i - 34];
						this.hips.GetComponentsInChildren<Transform>()[i].localRotation = this.animationFrames[0].bottomHalfQuaternions[i - 34];
					}
				}

				while (positionFrames.Count > 0 && positionFrames[0].positionFrame <= animationFrames[0].animFrame) {
					LerpPosition(positionFrames[0].vectors, positionFrames[0].quaternions, 9999999);
					positionFrames.RemoveAt(0);
				}

				//debugWriter.WriteLine("Next frame currently " + this.animationFrames.Count + " frames delayed, called recursively: " + recursive.ToString() + " offset: " + offset.ToString() + " frame time: " + this.animationFrames[0].deltaTime);

				float oldTime = this.animationFrames[0].timeSinceStart;
				float oldDelta = this.animationFrames[0].deltaTime;

				this.previousFrameTime = this.animationFrames[0].frameTime;
				this.animationFrames.RemoveAt(0);
				this.LerpNextFrame(true, oldTime - oldDelta);
			}
		}

		private void LerpPosition(Vector3[] vectors, Quaternion[] quaternions, float deltaTime) {
			this.player.transform.position = Vector3.Lerp(this.player.transform.position, vectors[0], deltaTime / this.animationFrames[0].deltaTime);
			this.player.transform.rotation = Quaternion.Slerp(this.player.transform.rotation, quaternions[0], deltaTime / this.animationFrames[0].deltaTime);

			this.board.transform.position = Vector3.Lerp(this.board.transform.position, vectors[1], deltaTime / this.animationFrames[0].deltaTime);
			this.board.transform.rotation = Quaternion.Slerp(this.board.transform.rotation, quaternions[1], deltaTime / this.animationFrames[0].deltaTime);

			this.skater.transform.position = Vector3.Lerp(this.skater.transform.position, vectors[2], deltaTime / this.animationFrames[0].deltaTime);
			this.skater.transform.rotation = Quaternion.Slerp(this.skater.transform.rotation, quaternions[2], deltaTime / this.animationFrames[0].deltaTime);

			this.usernameText.text = this.username;
			this.usernameObject.transform.position = this.player.transform.position + this.player.transform.up;
			this.usernameObject.transform.LookAt(Camera.main.transform);
		}
	}
}