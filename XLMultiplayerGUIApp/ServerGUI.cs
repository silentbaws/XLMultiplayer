using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XLMultiplayerServer;
using Valve.Sockets;

namespace XLMultiplayerGUIApp {
	public partial class ServerGUI : Form {
		private List<Tuple<string, ConsoleColor>> newLogQueue = new List<Tuple<string, ConsoleColor>>();
		private List<string> newChatMessages = new List<string>();
		private string _selectedMenuItem;
		private readonly ContextMenuStrip playerListMenu;
		private readonly ContextMenuStrip banListMenu;
		Server multiplayerServer;

		public void LogMessageCallbackHandler(string message, ConsoleColor color, params object[] objects) {
			if (objects != null && objects.Length > 0)
				newLogQueue.Add(Tuple.Create(String.Format(message, objects) + "\n", color));
			else
				newLogQueue.Add(Tuple.Create(message + "\n", color));
		}

		public void LogChatMessageCallback(string message) {
			newChatMessages.Add(message);
		}

		public ServerGUI() {
			InitializeComponent();

			var toolStripMenuItem1 = new ToolStripMenuItem { Text = "Kick Player" };
			var toolStripMenuItem2 = new ToolStripMenuItem { Text = "Ban Player" };
			toolStripMenuItem1.Click += playerToolMenuKick_Click;
			toolStripMenuItem2.Click += playerToolMenuBan_Click;

			playerListMenu = new ContextMenuStrip();
			playerListMenu.Items.AddRange(new ToolStripItem[] { toolStripMenuItem1, toolStripMenuItem2 });


			var banListMenuItem1 = new ToolStripMenuItem { Text = "Remove Ban" };
			banListMenuItem1.Click += banToolMenuRemove_Click;

			banListMenu = new ContextMenuStrip();
			banListMenu.Items.AddRange(new ToolStripItem[] { banListMenuItem1 });
		}

		private void ServerGUI_Load(object sender, EventArgs e) {
			LogMessage LogMessageCallback = LogMessageCallbackHandler;

			multiplayerServer = new Server(LogMessageCallback, LogChatMessageCallback);

			var serverTask = Task.Run(() => multiplayerServer.ServerLoop());
		}

		private void lsbPlayerList_MouseDown(object sender, MouseEventArgs e) {
			if (e.Button != MouseButtons.Right) return;
			var index = lsbPlayerList.IndexFromPoint(e.Location);
			if (index != ListBox.NoMatches) {
				_selectedMenuItem = lsbPlayerList.Items[index].ToString();
				playerListMenu.Show(Cursor.Position);
				playerListMenu.Visible = true;
			} else {
				playerListMenu.Visible = false;
			}
		}
		
		private void playerToolMenuKick_Click(object sender, EventArgs e) {
			string playerID = ParsePlayerIDMenuItem(_selectedMenuItem);

			DialogResult result = MessageBox.Show("Attempting to kick player " + _selectedMenuItem + " ID: " + playerID + " would you like to continue?", "Kicking Player", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

			if(result == DialogResult.Yes) {
				int targetID = Int32.Parse(playerID);
				Player target = multiplayerServer.players[targetID];

				if(target != null)
					multiplayerServer.RemovePlayer(target.connection, targetID, true);
			}
		}

		private void playerToolMenuBan_Click(object sender, EventArgs e) {
			string playerID = ParsePlayerIDMenuItem(_selectedMenuItem);

			DialogResult result = MessageBox.Show("Attempting to ban player " + _selectedMenuItem + " ID: " + playerID + " would you like to continue?", "Kicking Player", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

			if (result == DialogResult.Yes) {
				int targetID = Int32.Parse(playerID);
				
				multiplayerServer.BanPlayer(targetID);
			}
		}

		private string ParsePlayerIDMenuItem(string input) {
			int playerIDEnd = input.LastIndexOf(')');
			int playerIDStart = input.LastIndexOf('(');

			return _selectedMenuItem.Remove(playerIDEnd, 1).Remove(0, playerIDStart + 1);
		}

		private void lsbBanList_MouseDown(object sender, MouseEventArgs e) {
			if (e.Button != MouseButtons.Right) return;
			var index = lsbBanList.IndexFromPoint(e.Location);
			if (index != ListBox.NoMatches) {
				_selectedMenuItem = lsbBanList.Items[index].ToString();
				banListMenu.Show(Cursor.Position);
				banListMenu.Visible = true;
			} else {
				banListMenu.Visible = false;
			}
		}

		private void banToolMenuRemove_Click(object sender, EventArgs e) {
			var result = MessageBox.Show("You are about to remove a ban for the IP " + _selectedMenuItem + " would you like to continue?", "Remove Ban", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
			if (result == DialogResult.Yes) {
				multiplayerServer.RemoveBan(_selectedMenuItem);
			}
		}
		
		private void btnSendServerMessage_Click(object sender, EventArgs e) {
			byte[] serverMessageBytes = multiplayerServer.ProcessMessageCommand("msg:" + txtServerMessageDuration.Text + ":" + txtServerMessageColor.Text + " " + txtServerMessage.Text);
			
			foreach(Player player in multiplayerServer.players) {
				if(player != null) {
					multiplayerServer.server.SendMessageToConnection(player.connection, serverMessageBytes, SendFlags.Reliable);
				}
			}

			txtServerMessageDuration.Text = "";
			txtServerMessageColor.Text = "";
			txtServerMessage.Text = "";
		}

		private void btnReloadMapList_Click(object sender, EventArgs e) {
			multiplayerServer.LoadMapList();
		}

		private void tabPage5_Enter(object sender, EventArgs e) {
			txtMOTD.Text = multiplayerServer.motdString;
		}
		
		private void btnSetMOTD_Click(object sender, EventArgs e) {
			string newMOTD = "motd:" + txtMOTDDuration.Text + ":" + txtMOTDColor.Text + " " + txtMOTD.Text;
			byte[] newMOTDBytes = multiplayerServer.ProcessMessageCommand(newMOTD);
			if (newMOTDBytes != null)
				multiplayerServer.motdBytes = newMOTDBytes;
		}

		private void tmrUpdateLog_Tick(object sender, EventArgs e) {
			while (newLogQueue.Count > 0) {
				txtServerLog.AppendText(newLogQueue[0].Item1, FromColor(newLogQueue[0].Item2));
				newLogQueue.RemoveAt(0);
			}

			while(newChatMessages.Count > 0) {
				txtChatLog.AppendText(newChatMessages[0] + "\n");
				newChatMessages.RemoveAt(0);
			}

			lsbPlayerList.Items.Clear();

			if (multiplayerServer != null && multiplayerServer.players != null) {
				foreach (Player player in multiplayerServer.players) {
					if (player != null && player.username != null) lsbPlayerList.Items.Add(Server.RemoveMarkup(player.username) + "(" + player.playerID.ToString() + ")");
				}
			}

			lsbBanList.Items.Clear();
			if (multiplayerServer != null && multiplayerServer.bannedIPs != null) {
				lsbBanList.Items.AddRange(multiplayerServer.bannedIPs.ToArray());
			}
		}
		
		public static System.Drawing.Color FromColor(System.ConsoleColor c) {
			int[] cColors = {   0x000000, //Black = 0
                        0x000080, //DarkBlue = 1
                        0x008000, //DarkGreen = 2
                        0x008080, //DarkCyan = 3
                        0x800000, //DarkRed = 4
                        0x800080, //DarkMagenta = 5
                        0x808000, //DarkYellow = 6
                        0xC0C0C0, //Gray = 7
                        0x808080, //DarkGray = 8
                        0x0000FF, //Blue = 9
                        0x00FF00, //Green = 10
                        0x00FFFF, //Cyan = 11
                        0xFF0000, //Red = 12
                        0xFF00FF, //Magenta = 13
                        0xff8000, //Yellow = 14
                        0x000000  //White = 15
                    };
			return Color.FromArgb(cColors[(int)c]);
		}
	}

	public static class RichTextBoxExtensions {
		public static void AppendText(this RichTextBox box, string text, Color color) {
			box.SelectionStart = box.TextLength;
			box.SelectionLength = 0;

			box.SelectionColor = color;
			box.AppendText(text);
			box.SelectionColor = box.ForeColor;
		}
	}
}
