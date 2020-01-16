using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	class MultiplayerPlayerController {

		public MultiplayerPlayerController(StreamWriter writer) {
			// Set debug writer
		}

		// Override for local/remote players
		public virtual void ConstructPlayer() {
			
		}
	}
}
