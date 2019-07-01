using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace XLMultiplayer {
	public class MultiplayerPlayerController {
		public GameObject player { get; private set; }
		public GameObject skater { get; private set; }
		public GameObject board { get; private set; }

		public Animator animator { get; private set; }
		public Animator steezeAnimator { get; private set; }

		public int animBools { get; private set; }
		public int animFloats { get; private set; }
		public int animInts { get; private set; }

		public string[] animBoolNames { get; private set; }
		public string[] animFloatNames { get; private set; }
		public string[] animIntNames { get; private set; }

		public int animSteezeBools { get; private set; }
		public int animSteezeFloats { get; private set; }
		public int animSteezeInts { get; private set; }

		public string[] animSteezeBoolNames { get; private set; }
		public string[] animSteezeFloatNames { get; private set; }
		public string[] animSteezeIntNames { get; private set; }

		public string username = "IT ALL BROKE";

		private GameObject usernameObject;
		private TextMesh usernameText;

		private StreamWriter debugWriter;

		public int playerID;

		public MultiplayerPlayerController(StreamWriter writer) {
			this.debugWriter = writer;
		}

		public void ConstructForPlayer() {
			//Write the master prefab hierarchy to make sure everything is in place
			StreamWriter writer = new StreamWriter("Hierarchy.txt");
			foreach (Transform t in GameObject.Find("Master Prefab").GetComponentsInChildren<Transform>()) {
				Transform parent = t.parent;
				while (parent != null) {
					writer.Write("\t");
					parent = parent.parent;
				}
				writer.WriteLine("└─>" + t.name + (t.GetComponents<Rigidbody>().Length != 0 ? "<Contains rigidbody>" : ""));
			}
			writer.Close();

			//Get the skater root gameobject and set it to the player
			this.player = PlayerController.Instance.skaterController.skaterTransform.gameObject;
			Transform[] componentsInChildren = PlayerController.Instance.gameObject.GetComponentsInChildren<Transform>();
			bool foundSkater = false;

			//Get the actual skater and skateboard from the root object
			for (int i = 0; i < componentsInChildren.Length; i++) {
				if (componentsInChildren[i].gameObject.name.Equals("Skater")) {
					if (!foundSkater) {
						if (componentsInChildren[i].Find("Steeze IK")) {
							this.skater = componentsInChildren[i].gameObject;
							this.debugWriter.WriteLine("Found Skater");
							foundSkater = true;
						}
					}
				} else if (componentsInChildren[i].gameObject.name.Equals("Skateboard")) {
					this.board = componentsInChildren[i].gameObject;
					this.debugWriter.WriteLine("Found Board");
				}
			}

			if (!foundSkater) {
				this.debugWriter.WriteLine("Failed to find skater");
				return;
			}

			//Get all animators attached to the root
			Animator[] ourSkaterAnimators = new Animator[3];
			Array.Copy(this.skater.GetComponentsInChildren<Animator>(), ourSkaterAnimators, 2);
			ourSkaterAnimators[2] = PlayerController.Instance.animationController.ikAnim;

			if (ourSkaterAnimators[0] == null || ourSkaterAnimators[1] == null || ourSkaterAnimators[2] == null) {
				this.debugWriter.WriteLine("Failed to find an animator {0}, {1}, {2}", ourSkaterAnimators[0] == null, ourSkaterAnimators[1] == null, ourSkaterAnimators[2] == null);
				return;
			}

			//Set our animator and steeze animator
			this.animator = ourSkaterAnimators[0];
			this.steezeAnimator = ourSkaterAnimators[1];

			//Get the paramater names, types, and amount of each type from main animator and log all others
			this.animBools = 0;
			this.animFloats = 0;
			this.animInts = 0;
			for (int i = 0; i < ourSkaterAnimators.Length; i++) {
				debugWriter.WriteLine("Animator {0}: {1}, humanoid is {2}", i, ourSkaterAnimators[i].name, ourSkaterAnimators[i].isHuman);
				List<string> boolParams = new List<string>();
				List<string> floatParams = new List<string>();
				List<string> intParams = new List<string>();
				foreach (AnimatorControllerParameter param in ourSkaterAnimators[i].parameters) {
					if (param.type == AnimatorControllerParameterType.Bool) {
						if (i == 0)
							this.animBools++;
						else if (i == 1)
							this.animSteezeBools++;
						boolParams.Add(param.name);
					} else if (param.type == AnimatorControllerParameterType.Float) {
						if (i == 0)
							this.animFloats++;
						else if (i == 1)
							this.animSteezeFloats++;
						floatParams.Add(param.name);
					} else if (param.type == AnimatorControllerParameterType.Int) {
						if (i == 0)
							this.animInts++;
						else if (i == 1)
							this.animSteezeInts++;
						intParams.Add(param.name);
					}
				}
				debugWriter.Write("\tBoolean Paramaters: ");
				for (int c = 0; c < boolParams.ToArray().Length; c++)
					debugWriter.Write("\"{0}\", ", boolParams[c]);
				debugWriter.Write("\n\tFloat Paramaters: ");
				for (int c = 0; c < floatParams.ToArray().Length; c++)
					debugWriter.Write("\"{0}\", ", floatParams[c]);
				debugWriter.Write("\n\tInteger Paramaters: ");
				for (int c = 0; c < intParams.ToArray().Length; c++)
					debugWriter.Write("\"{0}\", ", intParams[c]);
				debugWriter.Write("\n");

				if (i == 0) {
					this.animBoolNames = boolParams.ToArray();
					this.animFloatNames = floatParams.ToArray();
					this.animIntNames = intParams.ToArray();
				} else if (i == 1) {
					this.animSteezeBoolNames = boolParams.ToArray();
					this.animSteezeFloatNames = floatParams.ToArray();
					this.animSteezeIntNames = intParams.ToArray();
				}

				boolParams.Clear();
				floatParams.Clear();
				intParams.Clear();
			}
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
			this.board = UnityEngine.Object.Instantiate<GameObject>(source.board);
			this.board.name = "New Player Board";
			this.board.transform.SetParent(this.player.transform, false);
			this.board.transform.localPosition = Vector3.zero;
			debugWriter.WriteLine("Created New Board");
			foreach (MonoBehaviour m in this.board.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = false;
				debugWriter.WriteLine("Removing script from additional board");
				UnityEngine.Object.DestroyImmediate(m);
			}

			//Copy the source players skater for our new player
			this.skater = UnityEngine.Object.Instantiate<GameObject>(source.skater);
			this.skater.name = "New Player Skater";
			this.skater.transform.SetParent(this.player.transform, false);
			this.skater.transform.localPosition = Vector3.zero;
			debugWriter.WriteLine("Created New Skater");
			foreach (MonoBehaviour m in this.skater.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = false;
				debugWriter.WriteLine("Removing script from additional skater");
				UnityEngine.Object.DestroyImmediate(m);
			}

			foreach (MonoBehaviour m in source.skater.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = true;
			}
			foreach (MonoBehaviour m in source.board.GetComponentsInChildren<MonoBehaviour>()) {
				m.enabled = true;
			}
			Time.timeScale = 1.0f;

			this.animBools = source.animBools;
			this.animFloats = source.animFloats;
			this.animInts = source.animInts;

			this.animBoolNames = source.animBoolNames;
			this.animFloatNames = source.animFloatNames;
			this.animIntNames = source.animIntNames;

			this.animSteezeBools = source.animSteezeBools;
			this.animSteezeFloats = source.animSteezeFloats;
			this.animSteezeInts = source.animSteezeInts;

			this.animSteezeBoolNames = source.animSteezeBoolNames;
			this.animSteezeFloatNames = source.animSteezeFloatNames;
			this.animSteezeIntNames = source.animSteezeIntNames;
			debugWriter.WriteLine("Set New Player Animation variables");

			//Get the animators on the new player
			Animator[] newSkaterAnimators = this.skater.GetComponentsInChildren<Animator>();
			this.animator = newSkaterAnimators[0];
			this.animator.enabled = true;
			this.steezeAnimator = newSkaterAnimators[1];
			newSkaterAnimators[1].enabled = true;
			debugWriter.WriteLine("Activated New Player Animators");

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

		public byte[] PackTransforms() {
			Transform[] T = new Transform[] { this.player.transform, this.board.transform, this.skater.transform };
			Rigidbody[] R = new Rigidbody[] { this.board.GetComponent<Rigidbody>(), this.board.GetComponent<Rigidbody>(), this.board.GetComponentsInChildren<Rigidbody>()[1], this.board.GetComponentsInChildren<Rigidbody>()[2] };

			byte[] packed = new byte[T.Length * 28 + R.Length * 40];
			for (int i = 0; i < T.Length; i++) {
				Array.Copy(BitConverter.GetBytes(T[i].position.x), 0, packed, i * 28, 4);
				Array.Copy(BitConverter.GetBytes(T[i].position.y), 0, packed, i * 28 + 4, 4);
				Array.Copy(BitConverter.GetBytes(T[i].position.z), 0, packed, i * 28 + 8, 4);
				Array.Copy(BitConverter.GetBytes(T[i].rotation.x), 0, packed, i * 28 + 12, 4);
				Array.Copy(BitConverter.GetBytes(T[i].rotation.y), 0, packed, i * 28 + 16, 4);
				Array.Copy(BitConverter.GetBytes(T[i].rotation.z), 0, packed, i * 28 + 20, 4);
				Array.Copy(BitConverter.GetBytes(T[i].rotation.w), 0, packed, i * 28 + 24, 4);
			}

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

		public void UnpackTransforms(byte[] buffer) {
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
			this.player.transform.position = vectors[0];
			this.player.transform.rotation = quaternions[0];
			this.board.transform.position = vectors[1];
			this.board.transform.rotation = quaternions[1];
			this.skater.transform.position = vectors[2];
			this.skater.transform.rotation = quaternions[2];
			this.skater.GetComponent<Rigidbody>().position = vectors[3];
			this.skater.GetComponent<Rigidbody>().velocity = vectors[4];
			this.skater.GetComponent<Rigidbody>().rotation = quaternions[3];
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

		public byte[] PackAnimator() {
			//Create arrays to hold the paramaters | packing 8 bools into a byte to save bandwidth for further expansion
			byte[] bools = new byte[(int)Math.Ceiling((double)this.animBools / 8)];
			byte[] floats = new byte[this.animFloats * 4];
			byte[] ints = new byte[this.animInts * 4];

			byte[] steezeBools = new byte[(int)Math.Ceiling((double)this.animSteezeBools / 8)];
			byte[] steezeFloats = new byte[this.animSteezeFloats * 4];
			byte[] steezeInts = new byte[this.animSteezeInts * 4];

			//Array to hold all paramater types in order
			byte[] packed = new byte[bools.Length + floats.Length + ints.Length + steezeBools.Length + steezeFloats.Length + steezeInts.Length];

			//Pack all individual paramater arrays
			int currentInt = 0;
			int currentBool = 0;
			int currentFloat = 0;
			foreach (AnimatorControllerParameter param in this.animator.parameters) {
				int index = param.nameHash;
				if (param.type == AnimatorControllerParameterType.Bool) {
					if (currentBool % 8 == 0)
						bools[(int)Math.Floor((double)currentBool / 8)] = 0;
					bools[(int)Math.Floor((double)currentBool / 8)] = (byte)(bools[(int)Math.Floor((double)currentBool / 8)] | (Convert.ToByte(this.animator.GetBool(index)) << (byte)(currentBool % 8)));
					currentBool++;
				} else if (param.type == AnimatorControllerParameterType.Float) {
					Array.Copy(BitConverter.GetBytes(this.animator.GetFloat(index)), 0, floats, currentFloat * 4, 4);
					currentFloat++;
				} else if (param.type == AnimatorControllerParameterType.Int) {
					Array.Copy(BitConverter.GetBytes(this.animator.GetInteger(index)), 0, ints, currentInt * 4, 4);
					currentInt++;
				}
			}

			currentInt = 0;
			currentBool = 0;
			currentFloat = 0;
			foreach (AnimatorControllerParameter param in this.steezeAnimator.parameters) {
				int index = param.nameHash;
				if (param.type == AnimatorControllerParameterType.Bool) {
					if (currentBool % 8 == 0)
						steezeBools[(int)Math.Floor((double)currentBool / 8)] = 0;
					steezeBools[(int)Math.Floor((double)currentBool / 8)] = (byte)(steezeBools[(int)Math.Floor((double)currentBool / 8)] | (Convert.ToByte(this.steezeAnimator.GetBool(index)) << (byte)(currentBool % 8)));
					currentBool++;
				} else if (param.type == AnimatorControllerParameterType.Float) {
					Array.Copy(BitConverter.GetBytes(this.steezeAnimator.GetFloat(index)), 0, steezeFloats, currentFloat * 4, 4);
					currentFloat++;
				} else if (param.type == AnimatorControllerParameterType.Int) {
					Array.Copy(BitConverter.GetBytes(this.steezeAnimator.GetInteger(index)), 0, steezeInts, currentInt * 4, 4);
					currentInt++;
				}
			}

			//Copy all paramaters into packed array in order
			Array.Copy(bools, packed, bools.Length);
			Array.Copy(floats, 0, packed, bools.Length, floats.Length);
			Array.Copy(ints, 0, packed, bools.Length + floats.Length, ints.Length);

			int steezeOffset = bools.Length + floats.Length + ints.Length;

			Array.Copy(steezeBools, 0, packed, steezeOffset, steezeBools.Length);
			Array.Copy(steezeFloats, 0, packed, steezeOffset + steezeBools.Length, steezeFloats.Length);
			Array.Copy(steezeInts, 0, packed, steezeOffset + steezeBools.Length + steezeInts.Length, steezeInts.Length);

			return packed;
		}

		public void UnpackAnimator(byte[] buffer) {
			bool[] bools = new bool[animBools];
			float[] floats = new float[animFloats];
			int[] ints = new int[animInts];

			for (int i = 0; i < animBools; i++) {
				bools[i] = Convert.ToBoolean((buffer[(int)Math.Floor((double)i / 8)] >> i % 8) & 0b1);
			}

			int floatOffset = (int)Math.Ceiling((double)animBools / 8);
			int intOffset = floatOffset + animFloats * 4;

			for (int i = 0; i < this.animFloats; i++) {
				floats[i] = BitConverter.ToSingle(buffer, i * 4 + floatOffset);
			}

			for (int i = 0; i < this.animInts; i++) {
				ints[i] = BitConverter.ToInt32(buffer, i * 4 + intOffset);
			}

			int steezeOffset = intOffset + this.animInts * 4;
			int steezeFloatOffset = steezeOffset + (int)Math.Ceiling((double)animSteezeBools / 8);
			int steezeIntOffset = steezeFloatOffset + this.animSteezeFloats * 4;

			bool[] steezeBools = new bool[animSteezeBools];
			float[] steezeFloats = new float[animSteezeFloats];
			int[] steezeInts = new int[animSteezeInts];

			for (int i = 0; i < animSteezeBools; i++) {
				steezeBools[i] = Convert.ToBoolean((buffer[(int)Math.Floor((double)i / 8) + steezeOffset] >> i % 8) & 0b1);
			}

			for (int i = 0; i < this.animSteezeFloats; i++) {
				steezeFloats[i] = BitConverter.ToSingle(buffer, i * 4 + steezeFloatOffset);
			}

			for (int i = 0; i < this.animSteezeInts; i++) {
				steezeInts[i] = BitConverter.ToInt32(buffer, i * 4 + steezeIntOffset);
			}

			SetAnimator(bools, floats, ints, steezeBools, steezeFloats, steezeInts);
		}

		public void SetAnimator(bool[] bools, float[] floats, int[] ints, bool[] steezeBools, float[] steezeFloats, int[] steezeInts) {
			for (int i = 0; i < this.animBools; i++)
				this.animator.SetBool(this.animBoolNames[i], bools[i]);
			for (int i = 0; i < this.animFloats; i++)
				this.animator.SetFloat(this.animFloatNames[i], floats[i]);
			for (int i = 0; i < this.animInts; i++)
				this.animator.SetInteger(this.animIntNames[i], ints[i]);
			for (int i = 0; i < this.animSteezeBools; i++)
				this.steezeAnimator.SetBool(this.animSteezeBoolNames[i], steezeBools[i]);
			for (int i = 0; i < this.animSteezeFloats; i++)
				this.steezeAnimator.SetFloat(this.animSteezeFloatNames[i], steezeFloats[i]);
			for (int i = 0; i < this.animSteezeInts; i++)
				this.steezeAnimator.SetInteger(this.animSteezeIntNames[i], steezeInts[i]);
		}
	}
}