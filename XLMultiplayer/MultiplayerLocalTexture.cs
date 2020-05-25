using System;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace XLMultiplayer {
	public class MultiplayerLocalTexture : MultiplayerTexture {
		public MultiplayerLocalTexture(bool custom, string path, MPTextureType texType, StreamWriter sw) : base(custom, path, texType, sw) { }
		public MultiplayerLocalTexture(MPTextureType texType, StreamWriter sw) : base(texType, sw) { }

		public void ConvertTexture(int maxSize = 1024) {
			Texture2D texture2D = null;

			if (this.isCustom && File.Exists(path)) {
				texture2D = new Texture2D(0, 0, TextureFormat.RGBA32, false);

				texture2D.LoadImage(File.ReadAllBytes(this.path));

				if (texture2D.width <= 4096 && texture2D.height <= 4096) {
					if (texture2D.width > maxSize || texture2D.height > maxSize)
						TextureScale.Bilinear(texture2D, maxSize, maxSize);
				} else {
					texture2D = null;
				}
			} else if (!this.isCustom && !this.path.Equals("")) {
				Texture texture = Resources.Load<Texture>(this.path);
				if (texture != null && texture.width <= 4096 && texture.height <= 4096) {
					texture2D = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);

					RenderTexture currentRT = RenderTexture.active;

					RenderTexture renderTexture = new RenderTexture(texture.width, texture.height, 32);
					Graphics.Blit(texture, renderTexture);

					RenderTexture.active = renderTexture;
					texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
					texture2D.Apply();

					if (texture2D.width > maxSize || texture2D.height > maxSize)
						TextureScale.Bilinear(texture2D, maxSize, maxSize);

					RenderTexture.active = currentRT;
				}
			}

			this.bytes = texture2D == null ? new byte[1] { 0 } : texture2D.EncodeToJPG(80);
		}
		
		public byte[] GetSendData() {
			byte[] sendBuffer = new byte[this.bytes.Length + 3];

			Array.Copy(this.bytes, 0, sendBuffer, 3, this.bytes.Length);
			sendBuffer[0] = (byte)OpCode.Texture;
			sendBuffer[1] = (byte)this.textureType;
			sendBuffer[2] = useFull ? (byte)1 : (byte)0;

			return sendBuffer;
		}
	}
}
