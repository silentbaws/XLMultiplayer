using Harmony12;
using ReplayEditor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerLocalPlayerController : MultiplayerPlayerController {

		// Get the character customizer
		public CharacterCustomizer characterCustomizer {
			get {
				if (_characterCustomizer == null) {
					_characterCustomizer = this.player.GetComponentInChildren<CharacterCustomizer>();
				}
				return _characterCustomizer;
			}
		}

		// Get the gear list on the character customizer
		public List<Tuple<CharacterGear, GameObject>> gearList {
			get {
				return Traverse.Create(characterCustomizer).Field("equippedGear").GetValue() as List<Tuple<CharacterGear, GameObject>>;
			}
		}

		private CharacterCustomizer _characterCustomizer;

		private bool startedEncoding = false;
		private bool copiedTextures = false;

		public MultiplayerLocalPlayerController(StreamWriter writer) : base(writer) {  }

		public System.Collections.IEnumerator EncodeTextures() {
			if (!this.startedEncoding) {
				this.startedEncoding = true;
				Main.statusMenu.isLoading = true;
				Main.statusMenu.loadingStatus = 0;
				yield return new WaitForEndOfFrame();

				this.shirtMPTex.ConvertAndSaveTexture();
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();

				this.pantsMPTex.ConvertAndSaveTexture();
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();

				this.shoesMPTex.ConvertAndSaveTexture();
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();

				this.hatMPTex.ConvertAndSaveTexture();
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();

				this.boardMPTex.ConvertAndSaveTexture();
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();

				copiedTextures = true;
				Main.statusMenu.loadingStatus++;
				yield return new WaitForEndOfFrame();
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

			foreach (Tuple<CharacterGear, GameObject> tup in gearList) {
				switch (tup.Item1.categoryName) {
					case "Shirt":
						this.shirtMPTex = new MultiplayerTexture(tup.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Shirt, this.debugWriter);
						this.shirtMPTex.useFull = false;
						break;
					case "Hoodie":
						this.shirtMPTex = new MultiplayerTexture(tup.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Shirt, this.debugWriter);
						this.shirtMPTex.useFull = true;
						break;
					case "Hat":
						this.hatMPTex = new MultiplayerTexture(tup.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Hat, this.debugWriter);
						break;
					case "Pants":
						this.pantsMPTex = new MultiplayerTexture(tup.Item2.GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Pants, this.debugWriter);
						break;
					case "Shoes":
						this.shoesMPTex = new MultiplayerTexture(tup.Item2.transform.Find("Shoe_R").GetComponent<Renderer>().material.GetTexture(MainTextureName), MPTextureType.Shoes, this.debugWriter);
						break;
				}
			}

			this.debugWriter.WriteLine("Finished constructing local player");
		}

		public byte[] PackTransformInfoArray(List<ReplayRecordedFrame> frames, int start, bool useKey) {
			return null;
		}

		public byte[] PackAnimations() {
			return null;
		}
	}
}
