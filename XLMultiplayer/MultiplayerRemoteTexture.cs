using System;
using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerRemoteTexture : MultiplayerTexture {
		public MultiplayerRemoteTexture(bool custom, string path, MPTextureType texType, StreamWriter sw) : base(custom, path, texType, sw) { }
		public MultiplayerRemoteTexture(MPTextureType texType, StreamWriter sw) : base(texType, sw) { }

		public bool useTexture { private set; get; } = true;
		public bool loaded = false;

		public string fileLocation { private set; get; }
		
		public void SaveTexture(int connectionId, byte[] buffer) {
			this.debugWriter.WriteLine("Saving texture in queue");
			this.useFull = buffer[1] == 1 ? true : false;
			byte[] file = new byte[buffer.Length - 2];
			Array.Copy(buffer, 2, file, 0, file.Length);

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
				controller.SetPlayerTexture(this.fileLocation, textureType, useFull);
			}
			loaded = true;
		}
	}
}
