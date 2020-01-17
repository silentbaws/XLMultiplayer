using System;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

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

		Texture texture;
		Texture2D texture2d;

		StreamWriter debugWriter;

		bool useTexture = true;

		string file;
		public bool loaded = false;
		public bool saved = false;

		// TODO: update byte math because I don't save size anymore

		public MultiplayerTexture(Texture tex, MPTextureType texType, StreamWriter sw) {
			textureType = texType;
			texture = tex;
		}

		public MultiplayerTexture(StreamWriter sw, MPTextureType t) {
			this.debugWriter = sw;
			textureType = t;
		}

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
			saved = true;
		}

		public void LoadFromFileMainThread(MultiplayerPlayerController controller) {
			if (useTexture) {
				debugWriter.WriteLine("LOADING TEXTURE FROM MAIN THREAD");
				byte[] data = File.ReadAllBytes(file);
				texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
				texture2d.LoadImage(data);
				controller.SetTexture(texture2d, textureType, useFull);
				loaded = true;
			} else if (textureType == MPTextureType.Shirt) {
				controller.SetTexture(null, MPTextureType.Shirt, useFull);
			}
		}

		public void SaveTexture(int connectionId, byte[] buffer) {
			debugWriter.WriteLine("Saving texture in queue");
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
