using Harmony12;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public abstract class MultiplayerPlayerController {
		public GameObject player { get; protected set; }

		public string username = "";

		protected StreamWriter debugWriter;
		
		private CharacterCustomizer _characterCustomizer = null;

		protected int numBones = 0;

		// Get the character customizer
		public CharacterCustomizer characterCustomizer {
			get {
				if (_characterCustomizer == null) {
					if(this.player.transform.parent != null)
						_characterCustomizer = this.player.transform.parent.GetComponentInChildren<CharacterCustomizer>();
					else
						_characterCustomizer = this.player.transform.GetComponentInChildren<CharacterCustomizer>();
				}
				return _characterCustomizer;
			}
		}
		
		// Get the gear list on the character customizer
		public List<ClothingGearObjet> gearList {
			get {
				return Traverse.Create(characterCustomizer).Field("equippedGear").GetValue() as List<ClothingGearObjet>;
			}
		}

		public List<BoardGearObject> boardGearList {
			get {
				return Traverse.Create(characterCustomizer).Field("equippedBoardGear").GetValue() as List<BoardGearObject>;
			}
		}

		public CharacterBodyObject currentBody {
			get {
				return Traverse.Create(characterCustomizer).Field("currentBody").GetValue() as CharacterBodyObject;
			}
			set {
				Traverse.Create(characterCustomizer).Field("currentBody").SetValue(value);
			}
		}

		public MultiplayerPlayerController(StreamWriter writer) {
			this.debugWriter = writer;
		}

		// Override for local/remote players
		public virtual void ConstructPlayer() {
			
		}
	}
}
