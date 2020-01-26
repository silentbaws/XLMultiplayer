using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerPlayerController {
		public GameObject player { get; protected set; }

		protected StreamWriter debugWriter;

		protected readonly string[] SkateboardMaterials = new string[] { "GripTape", "Deck", "Hanger", "Wheel1 Mesh", "Wheel2 Mesh", "Wheel3 Mesh", "Wheel4 Mesh" };

		protected const string MainTextureName = "Texture2D_4128E5C7";
		protected const string MainDeckTextureName = "Texture2D_694A07B4";

		public MultiplayerPlayerController(StreamWriter writer) {
			debugWriter = writer;
		}

		// Override for local/remote players
		public virtual void ConstructPlayer() {
			
		}
	}
}
