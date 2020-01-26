using Harmony12;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerPlayerController {
		public GameObject player { get; protected set; }

		public string username;

		protected StreamWriter debugWriter;

		protected readonly string[] SkateboardMaterials = new string[] { "GripTape", "Deck", "Hanger", "Wheel1 Mesh", "Wheel2 Mesh", "Wheel3 Mesh", "Wheel4 Mesh" };

		protected const string MainTextureName = "Texture2D_4128E5C7";
		protected const string MainDeckTextureName = "Texture2D_694A07B4";
		
		private CharacterCustomizer _characterCustomizer;

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

		public MultiplayerPlayerController(StreamWriter writer) {
			debugWriter = writer;
		}

		// Override for local/remote players
		public virtual void ConstructPlayer() {
			
		}
	}
}
