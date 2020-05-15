using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public enum MPTextureType : byte {
		Shirt = 0,
		Pants = 1,
		Shoes = 2,
		Hat = 3,
		Deck = 4,
		Grip = 5,
		Trucks = 6,
		Wheels = 7,
		Head = 8,
		Body = 9
	}

	public class MultiplayerTexture {
		public byte[] bytes = null;
		public MPTextureType textureType;

		public bool useFull = false;

		protected bool isCustom = false;
		protected string path = "";

		protected StreamWriter debugWriter;

		public bool saved = false;

		public MultiplayerTexture(bool custom, string path, MPTextureType texType, StreamWriter sw) {
			this.path = path;
			this.isCustom = custom;
			this.debugWriter = sw;
			this.textureType = texType;
		}
		
		public MultiplayerTexture(MPTextureType tex, StreamWriter sw) {
			this.debugWriter = sw;
			textureType = tex;
		}
	}
}
