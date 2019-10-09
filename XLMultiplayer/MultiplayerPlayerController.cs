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

		public byte[] PackTransformInfoArray(List<ReplayEditor.ReplayRecordedFrame> frames, int start, bool useKey) {
			ReplayEditor.TransformInfo[] T = frames[ReplayEditor.ReplayRecorder.Instance.RecordedFrames.Count - 1].transformInfos;
			ReplayEditor.TransformInfo[] TPrevious = frames[ReplayEditor.ReplayRecorder.Instance.RecordedFrames.Count - 2].transformInfos;

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

			byte[] transforms = PackTransformInfoArray(ReplayEditor.ReplayRecorder.Instance.RecordedFrames, 8, useKey);
			
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

			MultiplayerFrameBufferObject currentBufferObject = new MultiplayerFrameBufferObject();

			currentBufferObject.key = recBuffer[4] == (byte)1 ? true : false;
			currentBufferObject.animFrame = receivedPacketSequence;
			currentBufferObject.frameTime = BitConverter.ToSingle(buffer, buffer.Length - 4);

			List<Vector3> vectors = new List<Vector3>();
			List<Quaternion> quaternions = new List<Quaternion>();

			for (int i = 0; i < 68; i++) {
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

		public void LerpNextFrame(bool recursive = false, float offset = 0) {
			if (this.animationFrames.Count == 0 || this.animationFrames[0] == null) return;
			if (!startedAnimating && animationFrames.Count > 5) {
				if (this.animationFrames[0].vectors == null || !this.animationFrames[0].key) {
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
					this.hips.GetComponentsInChildren<Transform>()[i].localPosition = this.animationFrames[0].vectors[i];
					this.hips.GetComponentsInChildren<Transform>()[i].localRotation = this.animationFrames[0].quaternions[i];
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
					if (this.animationFrames[0].vectors != null) this.previousFrameTime = this.animationFrames[0].frameTime;
					if (this.animationFrames.Count == 6) break;
					//debugWriter.WriteLine("Removing frame currently still " + this.animationFrames.Count + " Frames behind");
				}
			}

			if (this.animationFrames[0].vectors == null) {
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
				this.hips.GetComponentsInChildren<Transform>()[i].localPosition = Vector3.Lerp(this.hips.GetComponentsInChildren<Transform>()[i].localPosition, this.animationFrames[0].vectors[i], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
				this.hips.GetComponentsInChildren<Transform>()[i].localRotation = Quaternion.Slerp(this.hips.GetComponentsInChildren<Transform>()[i].localRotation, this.animationFrames[0].quaternions[i], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
			}

			if (this.animationFrames[0].timeSinceStart >= this.animationFrames[0].deltaTime) {
				for (int i = 0; i < 68; i++) {
					this.hips.GetComponentsInChildren<Transform>()[i].localPosition = this.animationFrames[0].vectors[i];
					this.hips.GetComponentsInChildren<Transform>()[i].localRotation = this.animationFrames[0].quaternions[i];
				}

				while (positionFrames.Count > 0 && positionFrames[0].positionFrame <= animationFrames[0].animFrame) {
					LerpPosition(positionFrames[0].vectors, positionFrames[0].quaternions, 9999999);
					positionFrames.RemoveAt(0);
				}

				if (!this.animationFrames[1].key) {
					for(int i = 0; i < 68; i++) {
						this.animationFrames[1].vectors[i] = this.animationFrames[0].vectors[i] + this.animationFrames[1].vectors[i];
						this.animationFrames[1].quaternions[i].eulerAngles = this.animationFrames[0].quaternions[i].eulerAngles + this.animationFrames[1].quaternions[i].eulerAngles;
					}
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