using System;
using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerRemoteTexture : MultiplayerTexture {
		public MultiplayerRemoteTexture(bool custom, string path, string texType, GearInfoType gearType, StreamWriter sw) : base(custom, path, texType, gearType, sw) { }

		public bool useTexture { private set; get; } = true;
		public bool loaded = false;

		public string fileLocation { private set; get; }

		public void SaveTexture(int connectionId, byte[] buffer) {
			this.debugWriter.WriteLine("Saving texture in queue");
			
			if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing"))
				Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing");
			
			this.fileLocation = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + MultiplayerUtils.CalculateMD5Bytes(buffer) + connectionId.ToString() + ".jpg";

			try {
				File.WriteAllBytes(this.fileLocation, buffer);
			} catch (Exception e) {
				this.fileLocation = "";
				this.debugWriter.WriteLine(e.ToString());
			}
			
			this.saved = true;
			this.debugWriter.WriteLine("Saved texture in queue");
		}

		public void LoadFromFileMainThread(MultiplayerRemotePlayerController controller) {
			controller.SetPlayerTexture(this.fileLocation, textureType, infoType, useFull);
			loaded = true;
		}
	}
}
