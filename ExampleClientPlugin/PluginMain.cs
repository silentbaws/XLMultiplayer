using XLMultiplayer;

namespace ExampleClientPlugin {
    public class PluginMain {
		private static Plugin pluginInfo;

		private static void Load(Plugin plugin) {
			pluginInfo = plugin;
			pluginInfo.ProcessMessage = ReceiveMessage;
			pluginInfo.OnToggle = OnToggle;
		}

		private static void OnToggle(bool enabled) {
			if (enabled) {

			} else {

			}
		}

		private static void ReceiveMessage(byte[] message) {
			pluginInfo.SendMessage(pluginInfo, new byte[] { 5, 6, 7, 8 }, true);
		}
    }
}
