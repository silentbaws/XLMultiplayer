using System.IO;
using UnityEngine;

namespace XLMultiplayer {

	public class MultiplayerTexture {
		public byte[] bytes = null;
		public string textureType;
		public GearInfoType infoType;

		public bool isCustom { protected set; get; } = false;
		public string path { protected set; get; } = "";

		protected StreamWriter debugWriter;

		public bool saved = false;

		public MultiplayerTexture(bool custom, string path, string texType, GearInfoType gearType, StreamWriter sw) {
			this.path = path;
			this.isCustom = custom;
			this.debugWriter = sw;
			this.textureType = texType;
			this.infoType = gearType;
		}

		public MultiplayerTexture() {

		}
	}

	public enum GearInfoType {
		Clothing = 0,
		Board = 1,
		Body = 2
	}
}
