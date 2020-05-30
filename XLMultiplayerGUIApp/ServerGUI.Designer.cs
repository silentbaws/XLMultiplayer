namespace XLMultiplayerGUIApp {
	partial class ServerGUI {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.components = new System.ComponentModel.Container();
			this.lsbPlayerList = new System.Windows.Forms.ListBox();
			this.tmrUpdateLog = new System.Windows.Forms.Timer(this.components);
			this.txtServerLog = new System.Windows.Forms.RichTextBox();
			this.tabLogging = new System.Windows.Forms.TabControl();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.lblMessageColor = new System.Windows.Forms.Label();
			this.lblMessageDuration = new System.Windows.Forms.Label();
			this.lblServerMessage = new System.Windows.Forms.Label();
			this.btnSendServerMessage = new System.Windows.Forms.Button();
			this.txtServerMessageDuration = new System.Windows.Forms.TextBox();
			this.txtServerMessageColor = new System.Windows.Forms.TextBox();
			this.txtServerMessage = new System.Windows.Forms.TextBox();
			this.txtChatLog = new System.Windows.Forms.RichTextBox();
			this.tabPage5 = new System.Windows.Forms.TabPage();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.btnSetMOTD = new System.Windows.Forms.Button();
			this.txtMOTDDuration = new System.Windows.Forms.TextBox();
			this.txtMOTDColor = new System.Windows.Forms.TextBox();
			this.txtMOTD = new System.Windows.Forms.TextBox();
			this.btnReloadMapList = new System.Windows.Forms.Button();
			this.tabPlayerManagement = new System.Windows.Forms.TabControl();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.tabPage4 = new System.Windows.Forms.TabPage();
			this.lsbBanList = new System.Windows.Forms.ListBox();
			this.tabLogging.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage5.SuspendLayout();
			this.tabPlayerManagement.SuspendLayout();
			this.tabPage3.SuspendLayout();
			this.tabPage4.SuspendLayout();
			this.SuspendLayout();
			// 
			// lsbPlayerList
			// 
			this.lsbPlayerList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.lsbPlayerList.FormattingEnabled = true;
			this.lsbPlayerList.Location = new System.Drawing.Point(3, 3);
			this.lsbPlayerList.Name = "lsbPlayerList";
			this.lsbPlayerList.ScrollAlwaysVisible = true;
			this.lsbPlayerList.Size = new System.Drawing.Size(160, 394);
			this.lsbPlayerList.Sorted = true;
			this.lsbPlayerList.TabIndex = 1;
			this.lsbPlayerList.MouseDown += new System.Windows.Forms.MouseEventHandler(this.lsbPlayerList_MouseDown);
			// 
			// tmrUpdateLog
			// 
			this.tmrUpdateLog.Enabled = true;
			this.tmrUpdateLog.Interval = 16;
			this.tmrUpdateLog.Tick += new System.EventHandler(this.tmrUpdateLog_Tick);
			// 
			// txtServerLog
			// 
			this.txtServerLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtServerLog.Location = new System.Drawing.Point(3, 3);
			this.txtServerLog.Name = "txtServerLog";
			this.txtServerLog.ReadOnly = true;
			this.txtServerLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.ForcedVertical;
			this.txtServerLog.Size = new System.Drawing.Size(582, 398);
			this.txtServerLog.TabIndex = 3;
			this.txtServerLog.Text = "";
			// 
			// tabLogging
			// 
			this.tabLogging.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.tabLogging.Controls.Add(this.tabPage2);
			this.tabLogging.Controls.Add(this.tabPage1);
			this.tabLogging.Controls.Add(this.tabPage5);
			this.tabLogging.Location = new System.Drawing.Point(192, 12);
			this.tabLogging.Name = "tabLogging";
			this.tabLogging.SelectedIndex = 0;
			this.tabLogging.Size = new System.Drawing.Size(596, 430);
			this.tabLogging.TabIndex = 4;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this.txtServerLog);
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(588, 404);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Server Log";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// tabPage1
			// 
			this.tabPage1.Controls.Add(this.lblMessageColor);
			this.tabPage1.Controls.Add(this.lblMessageDuration);
			this.tabPage1.Controls.Add(this.lblServerMessage);
			this.tabPage1.Controls.Add(this.btnSendServerMessage);
			this.tabPage1.Controls.Add(this.txtServerMessageDuration);
			this.tabPage1.Controls.Add(this.txtServerMessageColor);
			this.tabPage1.Controls.Add(this.txtServerMessage);
			this.tabPage1.Controls.Add(this.txtChatLog);
			this.tabPage1.Location = new System.Drawing.Point(4, 22);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(588, 404);
			this.tabPage1.TabIndex = 2;
			this.tabPage1.Text = "Chat Log";
			this.tabPage1.UseVisualStyleBackColor = true;
			// 
			// lblMessageColor
			// 
			this.lblMessageColor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.lblMessageColor.AutoSize = true;
			this.lblMessageColor.Location = new System.Drawing.Point(382, 365);
			this.lblMessageColor.Name = "lblMessageColor";
			this.lblMessageColor.Size = new System.Drawing.Size(77, 13);
			this.lblMessageColor.TabIndex = 13;
			this.lblMessageColor.Text = "Message Color";
			// 
			// lblMessageDuration
			// 
			this.lblMessageDuration.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.lblMessageDuration.AutoSize = true;
			this.lblMessageDuration.Location = new System.Drawing.Point(328, 365);
			this.lblMessageDuration.Name = "lblMessageDuration";
			this.lblMessageDuration.Size = new System.Drawing.Size(47, 13);
			this.lblMessageDuration.TabIndex = 12;
			this.lblMessageDuration.Text = "Duration";
			// 
			// lblServerMessage
			// 
			this.lblServerMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.lblServerMessage.AutoSize = true;
			this.lblServerMessage.Location = new System.Drawing.Point(6, 365);
			this.lblServerMessage.Name = "lblServerMessage";
			this.lblServerMessage.Size = new System.Drawing.Size(50, 13);
			this.lblServerMessage.TabIndex = 11;
			this.lblServerMessage.Text = "Message";
			// 
			// btnSendServerMessage
			// 
			this.btnSendServerMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnSendServerMessage.Location = new System.Drawing.Point(491, 378);
			this.btnSendServerMessage.Name = "btnSendServerMessage";
			this.btnSendServerMessage.Size = new System.Drawing.Size(94, 23);
			this.btnSendServerMessage.TabIndex = 10;
			this.btnSendServerMessage.Text = "Send Message";
			this.btnSendServerMessage.UseVisualStyleBackColor = true;
			this.btnSendServerMessage.Click += new System.EventHandler(this.btnSendServerMessage_Click);
			// 
			// txtServerMessageDuration
			// 
			this.txtServerMessageDuration.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtServerMessageDuration.Location = new System.Drawing.Point(331, 381);
			this.txtServerMessageDuration.MaxLength = 2;
			this.txtServerMessageDuration.Name = "txtServerMessageDuration";
			this.txtServerMessageDuration.Size = new System.Drawing.Size(48, 20);
			this.txtServerMessageDuration.TabIndex = 8;
			// 
			// txtServerMessageColor
			// 
			this.txtServerMessageColor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtServerMessageColor.Location = new System.Drawing.Point(385, 381);
			this.txtServerMessageColor.MaxLength = 6;
			this.txtServerMessageColor.Name = "txtServerMessageColor";
			this.txtServerMessageColor.Size = new System.Drawing.Size(100, 20);
			this.txtServerMessageColor.TabIndex = 9;
			// 
			// txtServerMessage
			// 
			this.txtServerMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtServerMessage.Location = new System.Drawing.Point(6, 381);
			this.txtServerMessage.MaxLength = 1000;
			this.txtServerMessage.Name = "txtServerMessage";
			this.txtServerMessage.Size = new System.Drawing.Size(319, 20);
			this.txtServerMessage.TabIndex = 7;
			// 
			// txtChatLog
			// 
			this.txtChatLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtChatLog.Location = new System.Drawing.Point(3, 3);
			this.txtChatLog.Name = "txtChatLog";
			this.txtChatLog.ReadOnly = true;
			this.txtChatLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.ForcedVertical;
			this.txtChatLog.Size = new System.Drawing.Size(582, 359);
			this.txtChatLog.TabIndex = 6;
			this.txtChatLog.Text = "";
			// 
			// tabPage5
			// 
			this.tabPage5.Controls.Add(this.label1);
			this.tabPage5.Controls.Add(this.label2);
			this.tabPage5.Controls.Add(this.label3);
			this.tabPage5.Controls.Add(this.btnSetMOTD);
			this.tabPage5.Controls.Add(this.txtMOTDDuration);
			this.tabPage5.Controls.Add(this.txtMOTDColor);
			this.tabPage5.Controls.Add(this.txtMOTD);
			this.tabPage5.Controls.Add(this.btnReloadMapList);
			this.tabPage5.Location = new System.Drawing.Point(4, 22);
			this.tabPage5.Name = "tabPage5";
			this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage5.Size = new System.Drawing.Size(588, 404);
			this.tabPage5.TabIndex = 3;
			this.tabPage5.Text = "Server Utilities";
			this.tabPage5.UseVisualStyleBackColor = true;
			this.tabPage5.Enter += new System.EventHandler(this.tabPage5_Enter);
			// 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(382, 365);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(77, 13);
			this.label1.TabIndex = 20;
			this.label1.Text = "Message Color";
			// 
			// label2
			// 
			this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(328, 365);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(47, 13);
			this.label2.TabIndex = 19;
			this.label2.Text = "Duration";
			// 
			// label3
			// 
			this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(6, 365);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(50, 13);
			this.label3.TabIndex = 18;
			this.label3.Text = "Message";
			// 
			// btnSetMOTD
			// 
			this.btnSetMOTD.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnSetMOTD.Location = new System.Drawing.Point(491, 378);
			this.btnSetMOTD.Name = "btnSetMOTD";
			this.btnSetMOTD.Size = new System.Drawing.Size(94, 23);
			this.btnSetMOTD.TabIndex = 17;
			this.btnSetMOTD.Text = "Set MOTD";
			this.btnSetMOTD.UseVisualStyleBackColor = true;
			this.btnSetMOTD.Click += new System.EventHandler(this.btnSetMOTD_Click);
			// 
			// txtMOTDDuration
			// 
			this.txtMOTDDuration.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtMOTDDuration.Location = new System.Drawing.Point(331, 381);
			this.txtMOTDDuration.MaxLength = 2;
			this.txtMOTDDuration.Name = "txtMOTDDuration";
			this.txtMOTDDuration.Size = new System.Drawing.Size(48, 20);
			this.txtMOTDDuration.TabIndex = 15;
			// 
			// txtMOTDColor
			// 
			this.txtMOTDColor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtMOTDColor.Location = new System.Drawing.Point(385, 381);
			this.txtMOTDColor.MaxLength = 6;
			this.txtMOTDColor.Name = "txtMOTDColor";
			this.txtMOTDColor.Size = new System.Drawing.Size(100, 20);
			this.txtMOTDColor.TabIndex = 16;
			// 
			// txtMOTD
			// 
			this.txtMOTD.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.txtMOTD.Location = new System.Drawing.Point(6, 381);
			this.txtMOTD.MaxLength = 1000;
			this.txtMOTD.Name = "txtMOTD";
			this.txtMOTD.Size = new System.Drawing.Size(319, 20);
			this.txtMOTD.TabIndex = 14;
			// 
			// btnReloadMapList
			// 
			this.btnReloadMapList.Location = new System.Drawing.Point(6, 6);
			this.btnReloadMapList.Name = "btnReloadMapList";
			this.btnReloadMapList.Size = new System.Drawing.Size(133, 49);
			this.btnReloadMapList.TabIndex = 0;
			this.btnReloadMapList.Text = "Reload Map List";
			this.btnReloadMapList.UseVisualStyleBackColor = true;
			this.btnReloadMapList.Click += new System.EventHandler(this.btnReloadMapList_Click);
			// 
			// tabPlayerManagement
			// 
			this.tabPlayerManagement.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.tabPlayerManagement.Controls.Add(this.tabPage3);
			this.tabPlayerManagement.Controls.Add(this.tabPage4);
			this.tabPlayerManagement.Location = new System.Drawing.Point(12, 12);
			this.tabPlayerManagement.Name = "tabPlayerManagement";
			this.tabPlayerManagement.SelectedIndex = 0;
			this.tabPlayerManagement.Size = new System.Drawing.Size(174, 430);
			this.tabPlayerManagement.TabIndex = 5;
			// 
			// tabPage3
			// 
			this.tabPage3.Controls.Add(this.lsbPlayerList);
			this.tabPage3.Location = new System.Drawing.Point(4, 22);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage3.Size = new System.Drawing.Size(166, 404);
			this.tabPage3.TabIndex = 0;
			this.tabPage3.Text = "Player List";
			this.tabPage3.UseVisualStyleBackColor = true;
			// 
			// tabPage4
			// 
			this.tabPage4.Controls.Add(this.lsbBanList);
			this.tabPage4.Location = new System.Drawing.Point(4, 22);
			this.tabPage4.Name = "tabPage4";
			this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage4.Size = new System.Drawing.Size(166, 404);
			this.tabPage4.TabIndex = 1;
			this.tabPage4.Text = "Ban List";
			this.tabPage4.UseVisualStyleBackColor = true;
			// 
			// lsbBanList
			// 
			this.lsbBanList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
			this.lsbBanList.FormattingEnabled = true;
			this.lsbBanList.Location = new System.Drawing.Point(3, 3);
			this.lsbBanList.Name = "lsbBanList";
			this.lsbBanList.ScrollAlwaysVisible = true;
			this.lsbBanList.Size = new System.Drawing.Size(160, 394);
			this.lsbBanList.Sorted = true;
			this.lsbBanList.TabIndex = 6;
			this.lsbBanList.MouseDown += new System.Windows.Forms.MouseEventHandler(this.lsbBanList_MouseDown);
			// 
			// ServerGUI
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(800, 450);
			this.Controls.Add(this.tabPlayerManagement);
			this.Controls.Add(this.tabLogging);
			this.Name = "ServerGUI";
			this.Text = "XLMultiplayer Server";
			this.Load += new System.EventHandler(this.ServerGUI_Load);
			this.tabLogging.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage1.PerformLayout();
			this.tabPage5.ResumeLayout(false);
			this.tabPage5.PerformLayout();
			this.tabPlayerManagement.ResumeLayout(false);
			this.tabPage3.ResumeLayout(false);
			this.tabPage4.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.ListBox lsbPlayerList;
		private System.Windows.Forms.Timer tmrUpdateLog;
		private System.Windows.Forms.RichTextBox txtServerLog;
		private System.Windows.Forms.TabControl tabLogging;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.TabPage tabPage1;
		private System.Windows.Forms.TabControl tabPlayerManagement;
		private System.Windows.Forms.TabPage tabPage3;
		private System.Windows.Forms.TabPage tabPage4;
		private System.Windows.Forms.ListBox lsbBanList;
		private System.Windows.Forms.RichTextBox txtChatLog;
		private System.Windows.Forms.Label lblMessageColor;
		private System.Windows.Forms.Label lblMessageDuration;
		private System.Windows.Forms.Label lblServerMessage;
		private System.Windows.Forms.Button btnSendServerMessage;
		private System.Windows.Forms.TextBox txtServerMessageDuration;
		private System.Windows.Forms.TextBox txtServerMessageColor;
		private System.Windows.Forms.TextBox txtServerMessage;
		private System.Windows.Forms.TabPage tabPage5;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Button btnSetMOTD;
		private System.Windows.Forms.TextBox txtMOTDDuration;
		private System.Windows.Forms.TextBox txtMOTDColor;
		private System.Windows.Forms.TextBox txtMOTD;
		private System.Windows.Forms.Button btnReloadMapList;
	}
}

