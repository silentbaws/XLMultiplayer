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
		public MultiplayerLocalTexture boardMPTex;

		private bool startedEncoding = false;

		private int framesSinceKey = 5;

		public float currentAnimationTime { get; private set; }

		public MultiplayerLocalPlayerController(StreamWriter writer) : base(writer) {  }

		private System.Collections.IEnumerator IncrementLoading() {
			Main.statusMenu.loadingStatus++;
			yield return new WaitForEndOfFrame();
		}

		public System.Collections.IEnumerator EncodeTextures() {
			if (!this.startedEncoding) {
				this.startedEncoding = true;
				Main.statusMenu.isLoading = true;
				Main.statusMenu.loadingStatus = 0;
				yield return new WaitForEndOfFrame();

				this.shirtMPTex.ConvertTexture();
				IncrementLoading();

				this.pantsMPTex.ConvertTexture();
				IncrementLoading();

				this.shoesMPTex.ConvertTexture();
				IncrementLoading();

				this.hatMPTex.ConvertTexture();
				IncrementLoading();

				this.boardMPTex.ConvertTexture();
				IncrementLoading();

				IncrementLoading();
				yield return new WaitForEndOfFrame();

				// TODO: Send multiplayer textures

				yield return new WaitForEndOfFrame();

				Main.statusMenu.isLoading = false;
			}
			yield break;
		}

		public override void ConstructPlayer() {
			this.debugWriter.WriteLine("Constructing Local Player");

			this.player = PlayerController.Instance.skaterController.skaterTransform.gameObject;
			bool foundBoard = false;

			this.debugWriter.WriteLine("Found player, looking for board texture");

			//Get the the board from the skater
			foreach(Transform transform in PlayerController.Instance.gameObject.GetComponentInChildren<Transform>()) {
				if (transform.gameObject.name.Equals("Skateboard")) {
					foreach (Transform t2 in transform.GetComponentsInChildren<Transform>()) {
						if (t2.name.Equals(SkateboardMaterials[0])) {
							this.boardMPTex = new MultiplayerLocalTexture(t2.GetComponent<Renderer>().material.GetTexture(MainDeckTextureName), MPTextureType.Board, this.debugWriter);
							foundBoard = true;
							break;
						}
					}
				}

				if (foundBoard) {
					break;
				}
			}

			this.debugWriter.WriteLine("Got board texture, grabbing clothing textures");

			foreach (Tuple<CharacterGear, GameObject> tup in gearList) {
				switch (tup.Item1.categoryName) {
					case "Shirt":
						this.shirtMPTex = new MultiplayerLocalTexture(tup.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Shirt, this.debugWriter) {
							useFull = false
						};
						break;
					case "Hoodie":
						this.shirtMPTex = new MultiplayerLocalTexture(tup.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Shirt, this.debugWriter) {
							useFull = true
						};
						break;
					case "Hat":
						this.hatMPTex = new MultiplayerLocalTexture(tup.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Hat, this.debugWriter);
						break;
					case "Pants":
						this.pantsMPTex = new MultiplayerLocalTexture(tup.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Pants, this.debugWriter);
						break;
					case "Shoes":
						this.shoesMPTex = new MultiplayerLocalTexture(tup.Item2.transform.Find("Shoe_R").GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Shoes, this.debugWriter);
						break;
				}
			}

			this.debugWriter.WriteLine("Finished constructing local player");
		}

		public byte[] PackTransformInfoArray(List<ReplayRecordedFrame> frames, int start, bool useKey) {
			TransformInfo[] T = frames[ReplayRecorder.Instance.RecordedFrames.Count - 1].transformInfos;
			TransformInfo[] TPrevious = frames[ReplayRecorder.Instance.RecordedFrames.Count - 2].transformInfos;

			currentAnimationTime = frames[frames.Count].time;

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

			byte[] packed = new byte[transforms.Length + 5];

			packed[0] = useKey ? (byte)0 : (byte)1;
			Array.Copy(transforms, 0, packed, 1, transforms.Length);
			Array.Copy(BitConverter.GetBytes(ReplayRecorder.Instance.RecordedFrames[ReplayRecorder.Instance.RecordedFrames.Count - 1].time), 0, packed, transforms.Length + 1, 4);

			return new Tuple<byte[], bool>(packed, useKey);
		}
	}
}
