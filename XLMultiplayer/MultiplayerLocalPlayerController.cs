using ReplayEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XLMultiplayer {
	class MultiplayerLocalPlayerController : MultiplayerPlayerController {
		public MultiplayerLocalPlayerController(StreamWriter writer) : base(writer) {
		}

		public byte[] ConvertTexture(Texture tex, MPTextureType texType) {
			// Resize texture and encode as png
			return null;
		}

		public System.Collections.IEnumerator EncodeTextures() {
			// Convert Textures

			yield break;
		}

		public override void ConstructPlayer() {
			
		}

		public byte[] PackTransformInfoArray(List<ReplayRecordedFrame> frames, int start, bool useKey) {
			return null;
		}

		public byte[] PackAnimations() {
			return null;
		}
	}
}
