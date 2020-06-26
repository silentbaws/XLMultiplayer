using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace XLMultiplayer {
	public class Plugin {
		[JsonRequired]
		[JsonProperty("StartupMethod")]
		public string startMethod { get; private set; } = "";

		[JsonRequired]
		[JsonProperty("AssemblyName")]
		public string dllName { get; private set; } = "";

		public Assembly loadedDLL { get; private set; }

		public string hash { get; private set; } = "";

		public string path { get; private set; } = "";

		public byte pluginID { get; private set; } = 255;

		public bool enabled { get; private set; } = false;

		public Action<Plugin, byte[], bool> SendMessage { get; private set; }

		public Dictionary<string, string> mapList = new Dictionary<string, string>();

		public Action<bool> OnToggle;
		public Action<byte[]> ProcessMessage;

		public Plugin(string PluginDLL, string PluginStartMethod, string PluginPath, Action<Plugin, byte[], bool> Send) {
			dllName = PluginDLL;
			startMethod = PluginStartMethod;
			path = PluginPath;
			SendMessage = Send;
		}

		public void TogglePlugin(bool enabled) {
			if (this.enabled == enabled) return;
			this.enabled = enabled;
			OnToggle?.Invoke(enabled);
		}
	}
}
