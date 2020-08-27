using System;
using System.IO;
using UnityEngine;

namespace XLMultiplayer {
	public class MultiplayerRemoteTexture : MultiplayerTexture {
		public MultiplayerRemoteTexture(bool custom, string path, string texType, GearInfoType gearType, StreamWriter sw) : base(custom, path, texType, gearType, sw) { }

		public MultiplayerRemoteTexture(GearInfo gInfo, bool custom, StreamWriter sw) {
			this.info = gInfo;
			this.isCustom = custom;
			this.debugWriter = sw;
		}

		public bool useTexture { private set; get; } = true;
		public bool loaded = false;

		public GearInfo info;

		public void SaveTexture(int connectionId, byte[] buffer) {
			this.debugWriter.WriteLine("Saving texture in queue");

			if (isCustom && buffer != null) {
				if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing"))
					Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing");

				this.path = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp\\Clothing\\" + textureType.ToString() + MultiplayerUtils.CalculateMD5Bytes(buffer) + connectionId.ToString() + ".jpg";

				try {
					File.WriteAllBytes(this.path, buffer);
				} catch (Exception e) {
					this.path = "";
					this.debugWriter.WriteLine(e.ToString());
				}
			}
			
			this.saved = true;
			this.debugWriter.WriteLine("Saved texture in queue");
		}

		public void LoadFromFileMainThread(MultiplayerRemotePlayerController controller) {
			controller.SetPlayerTexture(this.path, textureType, infoType, this.isCustom, info);
			loaded = true;
		}
	}
}
