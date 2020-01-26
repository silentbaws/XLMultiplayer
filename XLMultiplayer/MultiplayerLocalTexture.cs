using System;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace XLMultiplayer {
	public class MultiplayerLocalTexture : MultiplayerTexture {
		public MultiplayerLocalTexture(Texture tex, MPTextureType texType, StreamWriter sw) : base(tex, texType, sw) { }
		public MultiplayerLocalTexture(MPTextureType texType, StreamWriter sw) : base(texType, sw) { }

		public void ConvertTexture() {
			Texture2D texture2D = null;
			if (this.texture.width <= 4096 && this.texture.height <= 4096) {
				texture2D = new Texture2D(this.texture.width, this.texture.height, TextureFormat.RGB24, false);

				RenderTexture currentRT = RenderTexture.active;

				RenderTexture renderTexture = new RenderTexture(this.texture.width, this.texture.height, 32);
				Graphics.Blit(this.texture, renderTexture);

				RenderTexture.active = renderTexture;
				texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
				texture2D.Apply();

				if (texture2D.width > 1024 || texture2D.height > 1024)
					TextureScale.Bilinear(texture2D, 1024, 1024);

				RenderTexture.active = currentRT;
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
