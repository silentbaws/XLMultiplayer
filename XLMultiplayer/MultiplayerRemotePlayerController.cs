using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerRemotePlayerController : MultiplayerPlayerController {
		public GameObject skater { get; private set; }
		public GameObject skaterMeshObjects { get; private set; }
		public GameObject board { get; private set; }
		
		public MultiplayerRemoteTexture shirtMPTex;
		public MultiplayerRemoteTexture pantsMPTex;
		public MultiplayerRemoteTexture shoesMPTex;
		public MultiplayerRemoteTexture hatMPTex;
		public MultiplayerRemoteTexture boardMPTex;

		public MultiplayerRemotePlayerController(StreamWriter writer) : base(writer) {  }

		override public void ConstructPlayer() {

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

		public void SetTexture(Texture tex, MPTextureType texType, bool useFull) {
			// Apply textures to player
		}

		public void UnpackAnimations(byte[] buffer) {

		}

		public void LerpNextFrame(bool inReplay, bool recursive = false, float offset = 0, int recursionLevel = 0) {

		}

		public void EnsureQuaternionListContinuity() {

		}
	}
}
