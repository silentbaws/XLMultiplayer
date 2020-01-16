using System;
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
		public Vector2 size;

		bool useFull = false;

		Texture2D texture;

		StreamWriter debugWriter;

		bool useTexture = true;

		string file;
		public bool loaded = false;
		public bool saved = false;

		public MultiplayerTexture(byte[] b, Vector2 s, MPTextureType t, StreamWriter sw) {
			bytes = b;
			size = s;
			textureType = t;
			string path = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp";
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			File.WriteAllBytes(path + "\\" + t.ToString() + ".jpg", b);
			saved = true;
		}

		public MultiplayerTexture(StreamWriter sw, MPTextureType t) {
			this.debugWriter = sw;
			textureType = t;
		}

		public void LoadFromFileMainThread(MultiplayerPlayerController controller) {
			if (useTexture) {
				debugWriter.WriteLine("LOADING TEXTURE FROM MAIN THREAD");
				byte[] data = File.ReadAllBytes(file);
				texture = new Texture2D((int)size.x, (int)size.y, TextureFormat.RGBA32, false);
				texture.LoadImage(data);
				controller.SetPlayerTexture(texture, textureType, useFull);
				loaded = true;
			} else if (textureType == MPTextureType.Shirt) {
				controller.SetPlayerTexture(null, MPTextureType.Shirt, useFull);
			}
		}

		public void SaveTexture(int connectionId, byte[] buffer) {
			debugWriter.WriteLine("Saving texture in queue");
			size = new Vector2(BitConverter.ToSingle(buffer, 3), BitConverter.ToSingle(buffer, 7));
			useFull = buffer[11] == 1 ? true : false;
			byte[] file = new byte[buffer.Length - 12];
			Array.Copy(buffer, 12, file, 0, file.Length);

			if (file.Length == 1) {
				useTexture = false;
			} else {
				if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing"))
					Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing");

				File.WriteAllBytes(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + connectionId.ToString() + ".jpg", file);

				this.file = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + connectionId.ToString() + ".jpg";
				saved = true;
				debugWriter.WriteLine("Saved texture in queue");
			}
		}
	}
}
