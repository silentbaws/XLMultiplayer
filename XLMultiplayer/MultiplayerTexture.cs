using System.IO;
using UnityEngine;

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

		public bool useFull = false;
		protected Texture texture;

		protected StreamWriter debugWriter;

		public bool saved = false;

		public MultiplayerTexture(Texture tex, MPTextureType texType, StreamWriter sw) {
			textureType = texType;
			texture = tex;
			debugWriter = sw;
		}

		public MultiplayerTexture(MPTextureType tex, StreamWriter sw) {
			this.debugWriter = sw;
			textureType = tex;
		}
	}
}
