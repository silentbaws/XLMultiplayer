using System;
using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerRemoteTexture : MultiplayerTexture {
		public MultiplayerRemoteTexture(Texture tex, MPTextureType texType, StreamWriter sw) : base(tex, texType, sw) { }
		public MultiplayerRemoteTexture(MPTextureType texType, StreamWriter sw) : base(texType, sw) { }

		private bool useTexture = true;
		public bool loaded = false;

		private Texture2D texture2d;

		private string fileLocation;

		// TODO: Redo byte math
		public void SaveTexture(int connectionId, byte[] buffer) {
			this.debugWriter.WriteLine("Saving texture in queue");
			this.useFull = buffer[11] == 1 ? true : false;
			byte[] file = new byte[buffer.Length - 12];
			Array.Copy(buffer, 12, file, 0, file.Length);

			if (file.Length == 1) {
				this.useTexture = false;
			} else {
				if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing"))
					Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing");

				File.WriteAllBytes(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + connectionId.ToString() + ".jpg", file);

				this.fileLocation = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + connectionId.ToString() + ".jpg";
				this.saved = true;
				this.debugWriter.WriteLine("Saved texture in queue");
			}
		}

		public void LoadFromFileMainThread(MultiplayerRemotePlayerController controller) {
			if (this.useTexture) {
				this.debugWriter.WriteLine("LOADING TEXTURE FROM MAIN THREAD");
				byte[] data = File.ReadAllBytes(this.fileLocation);
				this.texture2d = new Texture2D(1, 1, TextureFormat.RGBA32, false);
				this.texture2d.LoadImage(data);
				controller.SetTexture(texture2d, textureType, useFull);
				loaded = true;
			} else if (textureType == MPTextureType.Shirt) {
				controller.SetTexture(null, MPTextureType.Shirt, useFull);
			}
		}
	}
}
