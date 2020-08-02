using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace XLMultiplayer {
	public class MultiplayerLocalTexture : MultiplayerTexture {
		public MultiplayerLocalTexture(bool custom, string path, string texType, GearInfoType gearType, StreamWriter sw) : base(custom, path, texType, gearType, sw) { }

		public void ConvertTexture(int maxSize = 1024, bool convertToPNG = false) {
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

			if (texture2D == null) {
				this.bytes = new byte[] { 0 };
			} else {
				this.bytes = convertToPNG ? texture2D.EncodeToPNG() : texture2D.EncodeToJPG(80);
			}
			
		}
		
		public byte[] GetSendData() {
			// TODO: Update this to the full spec in MultiplayerTexture.cs

			List<byte> sendBuffer = new List<byte>();

			//sendBuffer[0] = isCustom ? (byte)1 : (byte)0;
			sendBuffer.Add(1);
			sendBuffer.Add((byte)infoType);

			if (isCustom || true) {
				byte[] typeString = Encoding.UTF8.GetBytes(this.textureType);
				byte[] typeLen = BitConverter.GetBytes((ushort)typeString.Length);
				byte[] dataLen = BitConverter.GetBytes(this.bytes.Length);

				sendBuffer.AddRange(typeLen);
				sendBuffer.AddRange(dataLen);
				sendBuffer.AddRange(typeString);
				sendBuffer.AddRange(this.bytes);
			}

			return sendBuffer.ToArray();
		}
	}
}
