using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace XLMultiplayer {
	public class MultiplayerLocalTexture : MultiplayerTexture {
		public MultiplayerLocalTexture(Texture tex, MPTextureType texType, StreamWriter sw) : base(tex, texType, sw) { }
		public MultiplayerLocalTexture(MPTextureType texType, StreamWriter sw) : base(texType, sw) { }

		private byte[] ConvertTexture(Texture t, MPTextureType texType) {
			Texture2D texture2D = null;
			if (t.width <= 4096 && t.height <= 4096) {
				texture2D = new Texture2D(t.width, t.height, TextureFormat.RGB24, false);

				RenderTexture currentRT = RenderTexture.active;

				RenderTexture renderTexture = new RenderTexture(t.width, t.height, 32);
				Graphics.Blit(t, renderTexture);

				RenderTexture.active = renderTexture;
				texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
				texture2D.Apply();

				if (texture2D.width > 1024 || texture2D.height > 1024)
					TextureScale.Bilinear(texture2D, 1024, 1024);

				RenderTexture.active = currentRT;
			}

			return texture2D == null ? new byte[1] { 0 } : texture2D.EncodeToJPG(80);
		}
		
		public void ConvertAndSaveTexture() {
			bytes = ConvertTexture(texture, textureType);

			string path = Directory.GetCurrentDirectory() + "\\Mods\\XLMultiplayer\\Temp";
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			File.WriteAllBytes(path + "\\" + textureType.ToString() + ".jpg", bytes);
			this.saved = true;
		}
	}
}
