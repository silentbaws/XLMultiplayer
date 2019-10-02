using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Threading;
using UnityModManagerNet;
using RootMotion.FinalIK;
using Harmony12;
using System.IO.Compression;
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
	}

	public class MultiplayerPlayerController {
		public GameObject player { get; private set; }
		public GameObject skater { get; private set; }
		public GameObject skaterMeshesObject { get; private set; }
		public GameObject board { get; private set; }

		public Transform hips;

		public GameObject hatObject, shirtObject, pantsObject, shoesObject, headArmsObject;
		
		public Vector3[] targetPositions = new Vector3[72];
		public Quaternion[] targetRotations = new Quaternion[72];

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

		public CharacterCustomizer characterCustomizer {
			get {
				if(_characterCustomizer == null) {
					_characterCustomizer = this.skater.GetComponent<CharacterCustomizer>();
				}
				return _characterCustomizer;
			}
		}

		public List<Tuple<CharacterGear, GameObject>> gearList {
			get {
				return Traverse.Create(characterCustomizer).Field("equippedGear").GetValue() as List<Tuple<CharacterGear, GameObject>>;
			}
		}

		private static CharacterCustomizer _characterCustomizer;
		
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

				if (texture2D.width > 2048 || texture2D.height > 2048)
					TextureScale.Bilinear(texture2D, 2048, 2048);

				Color[] pixels = texture2D.GetPixels();

				RenderTexture.active = currentRT;
			}

			return texture2D == null ? new byte[1] { 0 } : texture2D.EncodeToPNG();
		}

		public void SetPlayerTexture(Texture tex, MPTextureType texType, bool useFull) {
			switch (texType) {
				case MPTextureType.Pants:
					pantsObject.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
					break;
				case MPTextureType.Shirt:
					if (useFull) {
						GameObject.Destroy(headArmsObject);
						GameObject.Destroy(shirtObject);

						CharacterBody body = Traverse.Create(characterCustomizer).Field("characterBody").GetValue() as CharacterBody;

						Dictionary<string, Transform> bonesDict = this.hips.GetComponentsInChildren<Transform>().ToDictionary((Transform t) => t.name);

						GameObject g = Resources.Load<GameObject>("CharacterCustomization/Hoodie/PAX_1");
						shirtObject = CustomLoadSMRPrefab(g, skaterMeshesObject.transform, bonesDict);

						GameObject g2 = Resources.Load<GameObject>(body.headAndHandsPath);
						headArmsObject = CustomLoadSMRPrefab(g2, skaterMeshesObject.transform, bonesDict);
					}
					if(tex != null) shirtObject.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
					break;
				case MPTextureType.Shoes:
					GameObject Shoe_L = shoesObject.transform.Find("Shoe_L").gameObject;
					GameObject Shoe_R = shoesObject.transform.Find("Shoe_R").gameObject;

					Shoe_L.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
					Shoe_R.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
					break;
				case MPTextureType.Hat:
					hatObject.GetComponent<Renderer>().material.SetTexture(MainTextureName, tex);
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
			this.player = new GameObject();
			UnityEngine.Object.DontDestroyOnLoad(this.player);
			this.player.name = "New Player";
			this.player.transform.SetParent(null);
			this.player.transform.position = PlayerController.Instance.transform.position;
			debugWriter.WriteLine("Created New Player");

			Time.timeScale = 0.0f;
			foreach (MonoBehaviour m in source.skater.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = false;
			}
			foreach (MonoBehaviour m in source.board.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = false;
			}

			//Copy board from the source and reparent/rename it for the new player and remove all scripts
			//All scripts in the game use PlayerController.Instance and end up breaking the original character if left in
			//I'm also too lazy to convert every script to be multiplayer compatible hence why client state is just being copied
			this.board = UnityEngine.Object.Instantiate<GameObject>(source.board, this.player.transform, false);
			this.board.name = "New Player Board";
			this.board.transform.localPosition = Vector3.zero;
			debugWriter.WriteLine("Created New Board");
			foreach (MonoBehaviour m in this.board.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = false;
				debugWriter.WriteLine("Removing script from additional board");
				UnityEngine.Object.DestroyImmediate(m);
			}

			//Copy the source players skater for our new player
			this.skater = UnityEngine.Object.Instantiate<GameObject>(source.skater, this.player.transform, false);
			this.skater.name = "New Player Skater";
			this.skater.transform.localPosition = Vector3.zero;
			debugWriter.WriteLine("Created New Skater");
			foreach (MonoBehaviour m in this.skater.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = false;
				debugWriter.WriteLine("Removing script from additional skater");
				UnityEngine.Object.DestroyImmediate(m);
			}

			this.hips = this.skater.transform.Find("Skater_Joints").Find("Skater_root");

			foreach (Transform child in skater.transform) {
				foreach (Transform t in child) {
					if (t.name.StartsWith("PAX", true, null)) {
						this.skaterMeshesObject = child.gameObject;
						break;
					}
				}
				if (this.skaterMeshesObject != null) break;
			}

			foreach(Transform t in skaterMeshesObject.transform) {
				if (!t.name.Equals("eyes")) {
					GameObject.Destroy(t.gameObject);
				}
			}

			CharacterBody body = Traverse.Create(characterCustomizer).Field("characterBody").GetValue() as CharacterBody;

			Dictionary<string, Transform> bonesDict = this.hips.GetComponentsInChildren<Transform>().ToDictionary((Transform t) => t.name);

			GameObject g = Resources.Load<GameObject>("CharacterCustomization/Shirt/PAX_1");
			shirtObject = CustomLoadSMRPrefab(g, skaterMeshesObject.transform, bonesDict);

			GameObject g2 = Resources.Load<GameObject>(body.headAndArmsPath);
			headArmsObject = CustomLoadSMRPrefab(g2, skaterMeshesObject.transform, bonesDict);

			GameObject g3 = Resources.Load<GameObject>("CharacterCustomization/shoes/PAX_1");
			shoesObject = CustomLoadSMRPrefab(g3, skaterMeshesObject.transform, bonesDict);

			GameObject g4 = Resources.Load<GameObject>("CharacterCustomization/pants/PAX_1");
			pantsObject = CustomLoadSMRPrefab(g4, skaterMeshesObject.transform, bonesDict);

			GameObject g5 = Resources.Load<GameObject>("CharacterCustomization/Hat/PAX_1");
			hatObject = CustomLoadSMRPrefab(g5, skaterMeshesObject.transform, bonesDict);

			foreach (MonoBehaviour m in source.skater.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = true;
			}
			foreach (MonoBehaviour m in source.board.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = true;
			}
			Time.timeScale = 1.0f;

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

		public byte[] PackTransforms() {
			Transform[] T = new Transform[] { this.player.transform, this.board.transform, this.skater.transform };
			Rigidbody[] R = new Rigidbody[] { this.board.GetComponent<Rigidbody>(), this.board.GetComponent<Rigidbody>(), this.board.GetComponentsInChildren<Rigidbody>()[1], this.board.GetComponentsInChildren<Rigidbody>()[2] };

			byte[] packed = new byte[T.Length * 28 + R.Length * 40];
			byte[] TPacked = this.PackTransformArray(T);
			Array.Copy(TPacked, 0, packed, 0, TPacked.Length);

			for (int i = 0; i < R.Length; i++) {
				Array.Copy(BitConverter.GetBytes(R[i].position.x), 0, packed, T.Length * 28 + i * 40, 4);
				Array.Copy(BitConverter.GetBytes(R[i].position.y), 0, packed, T.Length * 28 + i * 40 + 4, 4);
				Array.Copy(BitConverter.GetBytes(R[i].position.z), 0, packed, T.Length * 28 + i * 40 + 8, 4);
				Array.Copy(BitConverter.GetBytes(R[i].rotation.x), 0, packed, T.Length * 28 + i * 40 + 12, 4);
				Array.Copy(BitConverter.GetBytes(R[i].rotation.y), 0, packed, T.Length * 28 + i * 40 + 16, 4);
				Array.Copy(BitConverter.GetBytes(R[i].rotation.z), 0, packed, T.Length * 28 + i * 40 + 20, 4);
				Array.Copy(BitConverter.GetBytes(R[i].rotation.w), 0, packed, T.Length * 28 + i * 40 + 24, 4);
				Array.Copy(BitConverter.GetBytes(R[i].velocity.x), 0, packed, T.Length * 28 + i * 40 + 28, 4);
				Array.Copy(BitConverter.GetBytes(R[i].velocity.y), 0, packed, T.Length * 28 + i * 40 + 32, 4);
				Array.Copy(BitConverter.GetBytes(R[i].velocity.z), 0, packed, T.Length * 28 + i * 40 + 36, 4);
			}

			return packed;
		}

		int outoforder = 0;
		int total = 0;

		public void UnpackTransforms(byte[] recBuffer) {
			int receivedPacketSequence = BitConverter.ToInt32(recBuffer, 0);
			total++;
			byte[] buffer = new byte[recBuffer.Length - 4];
			if (receivedPacketSequence < currentPositionPacket) {
				outoforder++;
				return;
			} else {
				currentPositionPacket = receivedPacketSequence;
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

			for (int i = 0; i < (buffer.Length - 28 * 3) / 40; i++) {
				Vector3 readVector = new Vector3();
				readVector.x = BitConverter.ToSingle(buffer, i * 40 + 28 * 3);
				readVector.y = BitConverter.ToSingle(buffer, i * 40 + 4 + 28 * 3);
				readVector.z = BitConverter.ToSingle(buffer, i * 40 + 8 + 28 * 3);
				vectors.Add(readVector);

				Quaternion readQuaternion = new Quaternion();
				readQuaternion.x = BitConverter.ToSingle(buffer, i * 40 + 12 + 28 * 3);
				readQuaternion.y = BitConverter.ToSingle(buffer, i * 40 + 16 + 28 * 3);
				readQuaternion.z = BitConverter.ToSingle(buffer, i * 40 + 20 + 28 * 3);
				readQuaternion.w = BitConverter.ToSingle(buffer, i * 40 + 24 + 28 * 3);
				quaternions.Add(readQuaternion);

				readVector = new Vector3();
				readVector.x = BitConverter.ToSingle(buffer, i * 40 + 28 + 28 * 3);
				readVector.y = BitConverter.ToSingle(buffer, i * 40 + 32 + 28 * 3);
				readVector.z = BitConverter.ToSingle(buffer, i * 40 + 36 + 28 * 3);
				vectors.Add(readVector);
			}

			SetTransforms(vectors.ToArray(), quaternions.ToArray());
		}

		public void SetTransforms(Vector3[] vectors, Quaternion[] quaternions) {
			MultiplayerPositionFrameBufferObject newPositionObject = new MultiplayerPositionFrameBufferObject();
			newPositionObject.vectors = vectors;
			newPositionObject.quaternions = quaternions;

			positionFrames.Add(newPositionObject);
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

		public byte[][] PackAnimator() {
			byte[][] packed = new byte[2][];

			byte[] transforms = PackTransformArray(this.hips.GetComponentsInChildren<Transform>());

			packed[0] = new byte[1014];
			packed[0][0] = 0;
			Array.Copy(transforms, 0, packed[0], 1, 1008);
			Array.Copy(BitConverter.GetBytes(Time.timeSinceLevelLoad), 0, packed[0], 1009, 4);
			packed[0][1013] = 0;

			packed[1] = new byte[1010];
			packed[1][0] = 1;
			Array.Copy(transforms, 1008, packed[1], 1, 1008);
			packed[1][1009] = 1;

			return packed;
		}

		public void UnpackAnimator(byte[] recBuffer) {
			int receivedPacketSequence = BitConverter.ToInt32(recBuffer, 0);

			byte[] buffer = new byte[recBuffer.Length - 5];
			if (receivedPacketSequence < currentAnimationPacket - 10) {
				return;
			} else {
				Array.Copy(recBuffer, 5, buffer, 0, recBuffer.Length - 5);
				currentAnimationPacket = receivedPacketSequence > currentAnimationPacket ? receivedPacketSequence : currentAnimationPacket;
			}

			int actualPacket = receivedPacketSequence;

			receivedPacketSequence = Mathf.FloorToInt(receivedPacketSequence / 2);

			MultiplayerFrameBufferObject currentBufferObject = null;
			bool isTop = buffer[buffer.Length - 1] == 0;
			bool isNew = false;

			foreach(MultiplayerFrameBufferObject animFrame in animationFrames) {
				if(animFrame.animFrame == receivedPacketSequence) {
					currentBufferObject = animFrame;
				}
			}

			if(currentBufferObject == null) {
				currentBufferObject = new MultiplayerFrameBufferObject();
				isNew = true;
			}

			currentBufferObject.animFrame = receivedPacketSequence;
			if (isTop) {
				currentBufferObject.frameTime = BitConverter.ToSingle(buffer, buffer.Length - 5);
			}

			List<Vector3> vectors = new List<Vector3>();
			List<Quaternion> quaternions = new List<Quaternion>();

			for (int i = 0; i < 36; i++) {
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

			if (isTop) {
				currentBufferObject.topHalfVectors = new Vector3[36];
				currentBufferObject.topHalfQuaternions = new Quaternion[36];
				for (int i = 0; i < 36; i++) {
					currentBufferObject.topHalfVectors[i] = vectors[i];
					currentBufferObject.topHalfQuaternions[i] = quaternions[i];
				}
			} else {
				currentBufferObject.bottomHalfVectors = new Vector3[36];
				currentBufferObject.bottomHalfQuaternions = new Quaternion[36];
				for (int i = 36; i < 72; i++) {
					currentBufferObject.bottomHalfVectors[i - 36] = vectors[i - 36];
					currentBufferObject.bottomHalfQuaternions[i - 36] = quaternions[i - 36];
				}
			}

			if (isNew) {
				this.animationFrames.Add(currentBufferObject);
			}
		}

		public void LerpNextFrame(bool recursive = false) {
			if (!startedAnimating && animationFrames.Count > 2) {
				if (this.animationFrames[0].topHalfVectors == null || this.animationFrames[0].bottomHalfVectors == null) {
					this.animationFrames.RemoveAt(0);
					return;
				}

				startedAnimating = true;

				for (int i = 0; i < 72; i++) {
					if (i < 36) {
						this.hips.GetComponentsInChildren<Transform>()[i].position = this.animationFrames[0].topHalfVectors[i];
						this.hips.GetComponentsInChildren<Transform>()[i].rotation = this.animationFrames[0].topHalfQuaternions[i];
					} else {
						this.hips.GetComponentsInChildren<Transform>()[i].position = this.animationFrames[0].bottomHalfVectors[i - 36];
						this.hips.GetComponentsInChildren<Transform>()[i].rotation = this.animationFrames[0].bottomHalfQuaternions[i - 36];
					}
				}

				LerpPosition(positionFrames[0].vectors, positionFrames[0].quaternions);
				positionFrames.RemoveAt(0);

				this.previousFrameTime = this.animationFrames[0].frameTime;

				this.animationFrames.RemoveAt(0);
			}
			if (!startedAnimating) return;

			if(this.animationFrames[0].topHalfVectors == null || this.animationFrames[0].bottomHalfVectors == null) {
				this.animationFrames.RemoveAt(0);

				LerpPosition(positionFrames[0].vectors, positionFrames[0].quaternions);
				this.positionFrames.RemoveAt(0);

				debugWriter.WriteLine("Skipping frame");
				return;
			}

			float offset = 0;

			if(this.animationFrames[0].deltaTime == 0) {
				this.animationFrames[0].deltaTime = this.animationFrames[0].frameTime - this.previousFrameTime;
				offset = this.animationFrames[0].timeSinceStart;
			}

			if(!recursive) this.animationFrames[0].timeSinceStart += Time.unscaledDeltaTime;

			for (int i = 0; i < 72; i++) {
				if (i < 36) {
					this.hips.GetComponentsInChildren<Transform>()[i].position = Vector3.Lerp(this.hips.GetComponentsInChildren<Transform>()[i].position, this.animationFrames[0].topHalfVectors[i], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
					this.hips.GetComponentsInChildren<Transform>()[i].rotation = Quaternion.Slerp(this.hips.GetComponentsInChildren<Transform>()[i].rotation, this.animationFrames[0].topHalfQuaternions[i], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
				} else {
					this.hips.GetComponentsInChildren<Transform>()[i].position = Vector3.Lerp(this.hips.GetComponentsInChildren<Transform>()[i].position, this.animationFrames[0].bottomHalfVectors[i - 36], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
					this.hips.GetComponentsInChildren<Transform>()[i].rotation = Quaternion.Slerp(this.hips.GetComponentsInChildren<Transform>()[i].rotation, this.animationFrames[0].bottomHalfQuaternions[i - 36], (recursive ? offset : Time.unscaledDeltaTime) / this.animationFrames[0].deltaTime);
				}
			}

			if (this.animationFrames[0].timeSinceStart >= this.animationFrames[0].deltaTime) {
				for (int i = 0; i < 72; i++) {
					if (i < 36) {
						this.hips.GetComponentsInChildren<Transform>()[i].position = this.animationFrames[0].topHalfVectors[i];
						this.hips.GetComponentsInChildren<Transform>()[i].rotation = this.animationFrames[0].topHalfQuaternions[i];
					} else {
						this.hips.GetComponentsInChildren<Transform>()[i].position = this.animationFrames[0].bottomHalfVectors[i - 36];
						this.hips.GetComponentsInChildren<Transform>()[i].rotation = this.animationFrames[0].bottomHalfQuaternions[i - 36];
					}
				}

				debugWriter.WriteLine("Next frame");

				LerpPosition(positionFrames[0].vectors, positionFrames[0].quaternions);
				positionFrames.RemoveAt(0);

				float oldTime = this.animationFrames[0].timeSinceStart;
				float oldDelta = this.animationFrames[0].deltaTime;

				this.previousFrameTime = this.animationFrames[0].frameTime;
				this.animationFrames.RemoveAt(0);
				this.animationFrames[0].timeSinceStart = oldTime - oldDelta;
				this.LerpNextFrame(true);
			}
		}

		private void LerpPosition(Vector3[] vectors, Quaternion[] quaternions) {
			this.player.transform.position = vectors[0];
			this.player.transform.rotation = quaternions[0];
			this.board.transform.position = vectors[1];
			this.board.transform.rotation = quaternions[1];
			this.skater.transform.position = vectors[2];
			this.skater.transform.rotation = quaternions[2];
			//this.skater.GetComponent<Rigidbody>().position = vectors[3];
			//this.skater.GetComponent<Rigidbody>().velocity = vectors[4];
			//this.skater.GetComponent<Rigidbody>().rotation = quaternions[3];
			Rigidbody[] boardBodies = this.board.GetComponentsInChildren<Rigidbody>();
			boardBodies[0].position = vectors[5];
			boardBodies[0].velocity = vectors[6];
			boardBodies[0].rotation = quaternions[4];
			boardBodies[1].position = vectors[7];
			boardBodies[1].velocity = vectors[8];
			boardBodies[1].rotation = quaternions[5];
			boardBodies[2].position = vectors[9];
			boardBodies[2].velocity = vectors[10];
			boardBodies[2].rotation = quaternions[6];

			this.usernameText.text = this.username;
			this.usernameObject.transform.position = this.player.transform.position + this.player.transform.up;
			this.usernameObject.transform.LookAt(Camera.main.transform);
		}
	}
}