using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	// New Texture Data Format
	// 1 Byte -> OpCode
	// 2 Bytes -> Body Type Length ( X Bytes )
	// X Bytes -> Body Type String
	// Individual Textures

	// Individual Texture Data
	// 1 Byte -> IsCustom
	// 1 Byte -> info type
	//		isCustom False:
	//			-> 2 Bytes -> Type Length ( X Bytes )
	//			-> 2 Bytes -> Path Length ( Y Bytes )
	//			-> X Bytes -> Type String
	//			-> Y Bytes -> Gear Path

	//		isCustom True:
	//			-> 2 Bytes -> Type Length ( X Bytes )
	//			-> 2 Bytes -> Hash Length ( Y Bytes )
	//			-> 4 Bytes -> Data Length ( Z Bytes )
	//			-> X Bytes -> Type String 
	//			-> Y Bytes -> Full Size Texture Hash String
	//			-> Z Bytes -> Texture Byte Data
	//			Excluding hash for first version

	// Compress/Send All Textures as one package

	public class MultiplayerTexture {
		public byte[] bytes = null;
		public string textureType;
		public GearInfoType infoType;

		public bool useFull = false;

		protected bool isCustom = false;
		protected string path = "";

		protected StreamWriter debugWriter;

		public bool saved = false;

		public MultiplayerTexture(bool custom, string path, string texType, GearInfoType gearType, StreamWriter sw) {
			this.path = path;
			this.isCustom = custom;
			this.debugWriter = sw;
			this.textureType = texType;
			this.infoType = gearType;
		}
	}

	public enum GearInfoType {
		Clothing = 0,
		Board = 1,
		Body = 2
	}
}
