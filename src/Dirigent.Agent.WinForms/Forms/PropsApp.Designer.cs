namespace Dirigent.Gui.WinForms
{
	partial class frmAppProperties
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.pageWindows = new System.Windows.Forms.TabPage();
			this.btnWindowsMinimize = new System.Windows.Forms.Button();
			this.btnWindowsMaximize = new System.Windows.Forms.Button();
			this.btnWindowsRefresh = new System.Windows.Forms.Button();
			this.btnWindowsHide = new System.Windows.Forms.Button();
			this.btnWindowsShow = new System.Windows.Forms.Button();
			this.lbWindows = new System.Windows.Forms.ListBox();
			this.pageProcessInfo = new System.Windows.Forms.TabPage();
			this.rtbProcInfo = new System.Windows.Forms.RichTextBox();
			this.pageAppDef = new System.Windows.Forms.TabPage();
			this.rtbAppDef = new System.Windows.Forms.RichTextBox();
			this.tabControl1.SuspendLayout();
			this.pageWindows.SuspendLayout();
			this.pageProcessInfo.SuspendLayout();
			this.pageAppDef.SuspendLayout();
			this.SuspendLayout();
			// 
			// tabControl1
			// 
			this.tabControl1.Controls.Add(this.pageAppDef);
			this.tabControl1.Controls.Add(this.pageWindows);
			this.tabControl1.Controls.Add(this.pageProcessInfo);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(800, 450);
			this.tabControl1.TabIndex = 1;
			this.tabControl1.Selecting += new System.Windows.Forms.TabControlCancelEventHandler(this.tabControl1_Selecting);
			// 
			// pageWindows
			// 
			this.pageWindows.Controls.Add(this.btnWindowsMinimize);
			this.pageWindows.Controls.Add(this.btnWindowsMaximize);
			this.pageWindows.Controls.Add(this.btnWindowsRefresh);
			this.pageWindows.Controls.Add(this.btnWindowsHide);
			this.pageWindows.Controls.Add(this.btnWindowsShow);
			this.pageWindows.Controls.Add(this.lbWindows);
			this.pageWindows.Location = new System.Drawing.Point(4, 34);
			this.pageWindows.Name = "pageWindows";
			this.pageWindows.Padding = new System.Windows.Forms.Padding(3);
			this.pageWindows.Size = new System.Drawing.Size(792, 412);
			this.pageWindows.TabIndex = 2;
			this.pageWindows.Text = "Windows";
			this.pageWindows.UseVisualStyleBackColor = true;
			// 
			// btnWindowsMinimize
			// 
			this.btnWindowsMinimize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnWindowsMinimize.Location = new System.Drawing.Point(672, 189);
			this.btnWindowsMinimize.Name = "btnWindowsMinimize";
			this.btnWindowsMinimize.Size = new System.Drawing.Size(112, 39);
			this.btnWindowsMinimize.TabIndex = 8;
			this.btnWindowsMinimize.Text = "Mi&nimize";
			this.btnWindowsMinimize.UseVisualStyleBackColor = true;
			this.btnWindowsMinimize.Click += new System.EventHandler(this.btnWindowsMinimize_Click);
			// 
			// btnWindowsMaximize
			// 
			this.btnWindowsMaximize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnWindowsMaximize.Location = new System.Drawing.Point(672, 134);
			this.btnWindowsMaximize.Name = "btnWindowsMaximize";
			this.btnWindowsMaximize.Size = new System.Drawing.Size(112, 39);
			this.btnWindowsMaximize.TabIndex = 7;
			this.btnWindowsMaximize.Text = "Ma&ximize";
			this.btnWindowsMaximize.UseVisualStyleBackColor = true;
			this.btnWindowsMaximize.Click += new System.EventHandler(this.btnWindowsMaximize_Click);
			// 
			// btnWindowsRefresh
			// 
			this.btnWindowsRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnWindowsRefresh.Location = new System.Drawing.Point(672, 331);
			this.btnWindowsRefresh.Name = "btnWindowsRefresh";
			this.btnWindowsRefresh.Size = new System.Drawing.Size(112, 39);
			this.btnWindowsRefresh.TabIndex = 6;
			this.btnWindowsRefresh.Text = "&Refresh";
			this.btnWindowsRefresh.UseVisualStyleBackColor = true;
			this.btnWindowsRefresh.Click += new System.EventHandler(this.btnWindowsRefresh_Click);
			// 
			// btnWindowsHide
			// 
			this.btnWindowsHide.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnWindowsHide.Location = new System.Drawing.Point(672, 74);
			this.btnWindowsHide.Name = "btnWindowsHide";
			this.btnWindowsHide.Size = new System.Drawing.Size(112, 39);
			this.btnWindowsHide.TabIndex = 5;
			this.btnWindowsHide.Text = "&Hide";
			this.btnWindowsHide.UseVisualStyleBackColor = true;
			this.btnWindowsHide.Click += new System.EventHandler(this.btnWindowsHide_Click);
			// 
			// btnWindowsShow
			// 
			this.btnWindowsShow.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnWindowsShow.Location = new System.Drawing.Point(672, 18);
			this.btnWindowsShow.Name = "btnWindowsShow";
			this.btnWindowsShow.Size = new System.Drawing.Size(112, 38);
			this.btnWindowsShow.TabIndex = 4;
			this.btnWindowsShow.Text = "&Show";
			this.btnWindowsShow.UseVisualStyleBackColor = true;
			this.btnWindowsShow.Click += new System.EventHandler(this.btnWindowsShow_Click);
			// 
			// lbWindows
			// 
			this.lbWindows.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.lbWindows.FormattingEnabled = true;
			this.lbWindows.ItemHeight = 25;
			this.lbWindows.Location = new System.Drawing.Point(0, 0);
			this.lbWindows.Name = "lbWindows";
			this.lbWindows.Size = new System.Drawing.Size(656, 404);
			this.lbWindows.TabIndex = 3;
			// 
			// pageProcessInfo
			// 
			this.pageProcessInfo.Controls.Add(this.rtbProcInfo);
			this.pageProcessInfo.Location = new System.Drawing.Point(4, 34);
			this.pageProcessInfo.Name = "pageProcessInfo";
			this.pageProcessInfo.Padding = new System.Windows.Forms.Padding(3);
			this.pageProcessInfo.Size = new System.Drawing.Size(792, 412);
			this.pageProcessInfo.TabIndex = 1;
			this.pageProcessInfo.Text = "Process Info";
			this.pageProcessInfo.UseVisualStyleBackColor = true;
			// 
			// rtbProcInfo
			// 
			this.rtbProcInfo.Dock = System.Windows.Forms.DockStyle.Fill;
			this.rtbProcInfo.Location = new System.Drawing.Point(3, 3);
			this.rtbProcInfo.Name = "rtbProcInfo";
			this.rtbProcInfo.ReadOnly = true;
			this.rtbProcInfo.Size = new System.Drawing.Size(786, 406);
			this.rtbProcInfo.TabIndex = 2;
			this.rtbProcInfo.Text = "";
			// 
			// pageAppDef
			// 
			this.pageAppDef.Controls.Add(this.rtbAppDef);
			this.pageAppDef.Location = new System.Drawing.Point(4, 34);
			this.pageAppDef.Name = "pageAppDef";
			this.pageAppDef.Padding = new System.Windows.Forms.Padding(3);
			this.pageAppDef.Size = new System.Drawing.Size(792, 412);
			this.pageAppDef.TabIndex = 0;
			this.pageAppDef.Text = "App Def";
			this.pageAppDef.UseVisualStyleBackColor = true;
			// 
			// rtbAppDef
			// 
			this.rtbAppDef.Dock = System.Windows.Forms.DockStyle.Fill;
			this.rtbAppDef.Location = new System.Drawing.Point(3, 3);
			this.rtbAppDef.Name = "rtbAppDef";
			this.rtbAppDef.ReadOnly = true;
			this.rtbAppDef.Size = new System.Drawing.Size(786, 406);
			this.rtbAppDef.TabIndex = 1;
			this.rtbAppDef.Text = "";
			// 
			// frmAppProperties
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(800, 450);
			this.Controls.Add(this.tabControl1);
			this.Name = "frmAppProperties";
			this.Text = "Properties";
			this.Load += new System.EventHandler(this.frmAppProperties_Load);
			this.tabControl1.ResumeLayout(false);
			this.pageWindows.ResumeLayout(false);
			this.pageProcessInfo.ResumeLayout(false);
			this.pageAppDef.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage pageAppDef;
		private System.Windows.Forms.RichTextBox rtbAppDef;
		private System.Windows.Forms.TabPage pageProcessInfo;
		private System.Windows.Forms.TabPage pageWindows;
		private System.Windows.Forms.RichTextBox rtbProcInfo;
		private System.Windows.Forms.Button btnWindowsHide;
		private System.Windows.Forms.Button btnWindowsShow;
		private System.Windows.Forms.ListBox lbWindows;
		private System.Windows.Forms.Button btnWindowsRefresh;
		private System.Windows.Forms.Button btnWindowsMinimize;
		private System.Windows.Forms.Button btnWindowsMaximize;
	}
}