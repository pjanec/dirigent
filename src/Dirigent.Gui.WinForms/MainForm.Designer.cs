namespace Dirigent.Gui.WinForms
{
	partial class frmMain
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
			if( disposing && ( components != null ) )
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
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
			this.statusStrip = new System.Windows.Forms.StatusStrip();
			this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
			this.menuMain = new System.Windows.Forms.MenuStrip();
			this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.exitToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.planToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.startPlanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.stopPlanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.restartPlanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.killPlanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
			this.selectPlanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.reloadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.reloadSharedConfigToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.killToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.killAllRunningAppsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.powerToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.rebootAllToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.shutdownAllToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.exitAndKillAppsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.exitAndLeaveAppsRunningToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.reinstallToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.reinstallManuallyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.onlineDocumentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.tmrTick = new System.Windows.Forms.Timer(this.components);
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPageApps = new System.Windows.Forms.TabPage();
			this.gridApps = new System.Windows.Forms.DataGridView();
			this.hdrName = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.hdrStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.hdrLaunchIcon = new System.Windows.Forms.DataGridViewImageColumn();
			this.hdrKillIcon = new System.Windows.Forms.DataGridViewImageColumn();
			this.hdrRestartIcon = new System.Windows.Forms.DataGridViewImageColumn();
			this.hdrEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
			this.hdrPlan = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.toolStripApps = new Dirigent.Gui.WinForms.MyToolStrip();
			this.btnSelectPlan = new System.Windows.Forms.ToolStripButton();
			this.btnStartPlan = new System.Windows.Forms.ToolStripButton();
			this.btnStopPlan = new System.Windows.Forms.ToolStripButton();
			this.btnKillPlan = new System.Windows.Forms.ToolStripButton();
			this.btnRestartPlan = new System.Windows.Forms.ToolStripButton();
			this.bntKillAll2 = new System.Windows.Forms.ToolStripButton();
			this.btnShowJustAppsFromCurrentPlan = new System.Windows.Forms.ToolStripButton();
			this.tabPagePlans = new System.Windows.Forms.TabPage();
			this.gridPlans = new System.Windows.Forms.DataGridView();
			this.hdrPlanName = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Status = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.hdrPlanStart = new System.Windows.Forms.DataGridViewImageColumn();
			this.hdrPlanStop = new System.Windows.Forms.DataGridViewImageColumn();
			this.hdrPlanKill = new System.Windows.Forms.DataGridViewImageColumn();
			this.hdrPlanRestart = new System.Windows.Forms.DataGridViewImageColumn();
			this.toolStripPlans = new Dirigent.Gui.WinForms.MyToolStrip();
			this.btnKillAll = new System.Windows.Forms.ToolStripButton();
			this.statusStrip.SuspendLayout();
			this.menuMain.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPageApps.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridApps)).BeginInit();
			this.toolStripApps.SuspendLayout();
			this.tabPagePlans.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridPlans)).BeginInit();
			this.toolStripPlans.SuspendLayout();
			this.SuspendLayout();
			// 
			// statusStrip
			// 
			this.statusStrip.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1});
			this.statusStrip.Location = new System.Drawing.Point(0, 597);
			this.statusStrip.Name = "statusStrip";
			this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 21, 0);
			this.statusStrip.Size = new System.Drawing.Size(1067, 22);
			this.statusStrip.TabIndex = 0;
			this.statusStrip.Text = "statusStrip1";
			// 
			// toolStripStatusLabel1
			// 
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			this.toolStripStatusLabel1.Size = new System.Drawing.Size(0, 13);
			// 
			// menuMain
			// 
			this.menuMain.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.menuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.planToolStripMenuItem,
            this.toolsToolStripMenuItem,
            this.helpToolStripMenuItem});
			this.menuMain.Location = new System.Drawing.Point(0, 0);
			this.menuMain.Name = "menuMain";
			this.menuMain.Padding = new System.Windows.Forms.Padding(9, 4, 0, 4);
			this.menuMain.Size = new System.Drawing.Size(1067, 42);
			this.menuMain.TabIndex = 1;
			this.menuMain.Text = "menuStrip1";
			// 
			// fileToolStripMenuItem
			// 
			this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem1});
			this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
			this.fileToolStripMenuItem.Size = new System.Drawing.Size(62, 34);
			this.fileToolStripMenuItem.Text = "File";
			// 
			// exitToolStripMenuItem1
			// 
			this.exitToolStripMenuItem1.Name = "exitToolStripMenuItem1";
			this.exitToolStripMenuItem1.Size = new System.Drawing.Size(164, 40);
			this.exitToolStripMenuItem1.Text = "Exit";
			this.exitToolStripMenuItem1.Click += new System.EventHandler(this.exitToolStripMenuItem1_Click);
			// 
			// planToolStripMenuItem
			// 
			this.planToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startPlanToolStripMenuItem,
            this.stopPlanToolStripMenuItem,
            this.restartPlanToolStripMenuItem,
            this.killPlanToolStripMenuItem,
            this.toolStripMenuItem1,
            this.selectPlanToolStripMenuItem});
			this.planToolStripMenuItem.Name = "planToolStripMenuItem";
			this.planToolStripMenuItem.Size = new System.Drawing.Size(71, 34);
			this.planToolStripMenuItem.Text = "Plan";
			// 
			// startPlanToolStripMenuItem
			// 
			this.startPlanToolStripMenuItem.Name = "startPlanToolStripMenuItem";
			this.startPlanToolStripMenuItem.Size = new System.Drawing.Size(195, 40);
			this.startPlanToolStripMenuItem.Text = "Start";
			this.startPlanToolStripMenuItem.Click += new System.EventHandler(this.startPlanMenuItem_Click);
			// 
			// stopPlanToolStripMenuItem
			// 
			this.stopPlanToolStripMenuItem.Name = "stopPlanToolStripMenuItem";
			this.stopPlanToolStripMenuItem.Size = new System.Drawing.Size(195, 40);
			this.stopPlanToolStripMenuItem.Text = "Stop";
			this.stopPlanToolStripMenuItem.Click += new System.EventHandler(this.stopPlanMenuItem_Click);
			// 
			// restartPlanToolStripMenuItem
			// 
			this.restartPlanToolStripMenuItem.Name = "restartPlanToolStripMenuItem";
			this.restartPlanToolStripMenuItem.Size = new System.Drawing.Size(195, 40);
			this.restartPlanToolStripMenuItem.Text = "Restart";
			this.restartPlanToolStripMenuItem.Click += new System.EventHandler(this.restartPlanMenuItem_Click);
			// 
			// killPlanToolStripMenuItem
			// 
			this.killPlanToolStripMenuItem.Name = "killPlanToolStripMenuItem";
			this.killPlanToolStripMenuItem.Size = new System.Drawing.Size(195, 40);
			this.killPlanToolStripMenuItem.Text = "Kill";
			this.killPlanToolStripMenuItem.Click += new System.EventHandler(this.killPlanMenuItem_Click);
			// 
			// toolStripMenuItem1
			// 
			this.toolStripMenuItem1.Name = "toolStripMenuItem1";
			this.toolStripMenuItem1.Size = new System.Drawing.Size(192, 6);
			// 
			// selectPlanToolStripMenuItem
			// 
			this.selectPlanToolStripMenuItem.Name = "selectPlanToolStripMenuItem";
			this.selectPlanToolStripMenuItem.Size = new System.Drawing.Size(195, 40);
			this.selectPlanToolStripMenuItem.Text = "Select";
			// 
			// toolsToolStripMenuItem
			// 
			this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.reloadToolStripMenuItem,
            this.killToolStripMenuItem1,
            this.powerToolStripMenuItem1,
            this.exitToolStripMenuItem,
            this.reinstallToolStripMenuItem});
			this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
			this.toolsToolStripMenuItem.Size = new System.Drawing.Size(78, 34);
			this.toolsToolStripMenuItem.Text = "Tools";
			// 
			// reloadToolStripMenuItem
			// 
			this.reloadToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.reloadSharedConfigToolStripMenuItem});
			this.reloadToolStripMenuItem.Name = "reloadToolStripMenuItem";
			this.reloadToolStripMenuItem.Size = new System.Drawing.Size(293, 40);
			this.reloadToolStripMenuItem.Text = "Reload";
			// 
			// reloadSharedConfigToolStripMenuItem
			// 
			this.reloadSharedConfigToolStripMenuItem.Name = "reloadSharedConfigToolStripMenuItem";
			this.reloadSharedConfigToolStripMenuItem.Size = new System.Drawing.Size(262, 40);
			this.reloadSharedConfigToolStripMenuItem.Text = "Shared Config";
			this.reloadSharedConfigToolStripMenuItem.Click += new System.EventHandler(this.reloadSharedConfigToolStripMenuItem_Click);
			// 
			// killToolStripMenuItem1
			// 
			this.killToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.killAllRunningAppsToolStripMenuItem});
			this.killToolStripMenuItem1.Name = "killToolStripMenuItem1";
			this.killToolStripMenuItem1.Size = new System.Drawing.Size(293, 40);
			this.killToolStripMenuItem1.Text = "Kill";
			// 
			// killAllRunningAppsToolStripMenuItem
			// 
			this.killAllRunningAppsToolStripMenuItem.Name = "killAllRunningAppsToolStripMenuItem";
			this.killAllRunningAppsToolStripMenuItem.Size = new System.Drawing.Size(292, 40);
			this.killAllRunningAppsToolStripMenuItem.Text = "All Running Apps";
			this.killAllRunningAppsToolStripMenuItem.Click += new System.EventHandler(this.killAllRunningAppsToolStripMenuItem_Click);
			// 
			// powerToolStripMenuItem1
			// 
			this.powerToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.rebootAllToolStripMenuItem1,
            this.shutdownAllToolStripMenuItem1});
			this.powerToolStripMenuItem1.Name = "powerToolStripMenuItem1";
			this.powerToolStripMenuItem1.Size = new System.Drawing.Size(293, 40);
			this.powerToolStripMenuItem1.Text = "Power";
			// 
			// rebootAllToolStripMenuItem1
			// 
			this.rebootAllToolStripMenuItem1.Name = "rebootAllToolStripMenuItem1";
			this.rebootAllToolStripMenuItem1.Size = new System.Drawing.Size(254, 40);
			this.rebootAllToolStripMenuItem1.Text = "Reboot All";
			this.rebootAllToolStripMenuItem1.Click += new System.EventHandler(this.rebootAllToolStripMenuItem1_Click);
			// 
			// shutdownAllToolStripMenuItem1
			// 
			this.shutdownAllToolStripMenuItem1.Name = "shutdownAllToolStripMenuItem1";
			this.shutdownAllToolStripMenuItem1.Size = new System.Drawing.Size(254, 40);
			this.shutdownAllToolStripMenuItem1.Text = "Shutdown All";
			this.shutdownAllToolStripMenuItem1.Click += new System.EventHandler(this.shutdownAllToolStripMenuItem1_Click);
			// 
			// exitToolStripMenuItem
			// 
			this.exitToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitAndKillAppsToolStripMenuItem,
            this.exitAndLeaveAppsRunningToolStripMenuItem});
			this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
			this.exitToolStripMenuItem.Size = new System.Drawing.Size(293, 40);
			this.exitToolStripMenuItem.Text = "Terminate Agents";
			// 
			// exitAndKillAppsToolStripMenuItem
			// 
			this.exitAndKillAppsToolStripMenuItem.Name = "exitAndKillAppsToolStripMenuItem";
			this.exitAndKillAppsToolStripMenuItem.Size = new System.Drawing.Size(312, 40);
			this.exitAndKillAppsToolStripMenuItem.Text = "Kill apps";
			this.exitAndKillAppsToolStripMenuItem.Click += new System.EventHandler(this.terminateAndKillAppsToolStripMenuItem_Click);
			// 
			// exitAndLeaveAppsRunningToolStripMenuItem
			// 
			this.exitAndLeaveAppsRunningToolStripMenuItem.Name = "exitAndLeaveAppsRunningToolStripMenuItem";
			this.exitAndLeaveAppsRunningToolStripMenuItem.Size = new System.Drawing.Size(312, 40);
			this.exitAndLeaveAppsRunningToolStripMenuItem.Text = "Leave apps running";
			this.exitAndLeaveAppsRunningToolStripMenuItem.Click += new System.EventHandler(this.terminateAndLeaveAppsRunningToolStripMenuItem_Click);
			// 
			// reinstallToolStripMenuItem
			// 
			this.reinstallToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.reinstallManuallyToolStripMenuItem});
			this.reinstallToolStripMenuItem.Name = "reinstallToolStripMenuItem";
			this.reinstallToolStripMenuItem.Size = new System.Drawing.Size(293, 40);
			this.reinstallToolStripMenuItem.Text = "Reinstall Agents";
			// 
			// reinstallManuallyToolStripMenuItem
			// 
			this.reinstallManuallyToolStripMenuItem.Name = "reinstallManuallyToolStripMenuItem";
			this.reinstallManuallyToolStripMenuItem.Size = new System.Drawing.Size(231, 40);
			this.reinstallManuallyToolStripMenuItem.Text = "Manually...";
			this.reinstallManuallyToolStripMenuItem.Click += new System.EventHandler(this.reinstallManuallyToolStripMenuItem_Click);
			// 
			// helpToolStripMenuItem
			// 
			this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem,
            this.onlineDocumentationToolStripMenuItem});
			this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
			this.helpToolStripMenuItem.Size = new System.Drawing.Size(74, 34);
			this.helpToolStripMenuItem.Text = "Help";
			// 
			// aboutToolStripMenuItem
			// 
			this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
			this.aboutToolStripMenuItem.Size = new System.Drawing.Size(342, 40);
			this.aboutToolStripMenuItem.Text = "&About";
			this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutMenuItem_Click);
			// 
			// onlineDocumentationToolStripMenuItem
			// 
			this.onlineDocumentationToolStripMenuItem.Name = "onlineDocumentationToolStripMenuItem";
			this.onlineDocumentationToolStripMenuItem.Size = new System.Drawing.Size(342, 40);
			this.onlineDocumentationToolStripMenuItem.Text = "Online Documentation";
			this.onlineDocumentationToolStripMenuItem.Click += new System.EventHandler(this.onlineDocumentationToolStripMenuItem_Click);
			// 
			// tmrTick
			// 
			this.tmrTick.Interval = 500;
			this.tmrTick.Tick += new System.EventHandler(this.tmrTick_Tick);
			// 
			// tabControl1
			// 
			this.tabControl1.Controls.Add(this.tabPageApps);
			this.tabControl1.Controls.Add(this.tabPagePlans);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 42);
			this.tabControl1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(1067, 555);
			this.tabControl1.TabIndex = 4;
			// 
			// tabPageApps
			// 
			this.tabPageApps.Controls.Add(this.gridApps);
			this.tabPageApps.Controls.Add(this.toolStripApps);
			this.tabPageApps.Location = new System.Drawing.Point(4, 39);
			this.tabPageApps.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPageApps.Name = "tabPageApps";
			this.tabPageApps.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPageApps.Size = new System.Drawing.Size(1059, 512);
			this.tabPageApps.TabIndex = 0;
			this.tabPageApps.Text = "Apps";
			this.tabPageApps.UseVisualStyleBackColor = true;
			// 
			// gridApps
			// 
			this.gridApps.AllowUserToAddRows = false;
			this.gridApps.AllowUserToDeleteRows = false;
			this.gridApps.BackgroundColor = System.Drawing.SystemColors.Window;
			this.gridApps.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.gridApps.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.hdrName,
            this.hdrStatus,
            this.hdrLaunchIcon,
            this.hdrKillIcon,
            this.hdrRestartIcon,
            this.hdrEnabled,
            this.hdrPlan});
			this.gridApps.Dock = System.Windows.Forms.DockStyle.Fill;
			this.gridApps.Location = new System.Drawing.Point(4, 30);
			this.gridApps.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.gridApps.MultiSelect = false;
			this.gridApps.Name = "gridApps";
			this.gridApps.ReadOnly = true;
			this.gridApps.RowHeadersVisible = false;
			this.gridApps.RowHeadersWidth = 72;
			this.gridApps.RowTemplate.Height = 24;
			this.gridApps.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.gridApps.Size = new System.Drawing.Size(1051, 477);
			this.gridApps.TabIndex = 6;
			this.gridApps.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.gridApps_CellFormatting);
			this.gridApps.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gridApps_MouseClick);
			this.gridApps.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.gridApps_MouseDoubleClick);
			// 
			// hdrName
			// 
			this.hdrName.HeaderText = "Application Name";
			this.hdrName.MinimumWidth = 9;
			this.hdrName.Name = "hdrName";
			this.hdrName.ReadOnly = true;
			this.hdrName.Width = 250;
			// 
			// hdrStatus
			// 
			this.hdrStatus.HeaderText = "Status";
			this.hdrStatus.MinimumWidth = 9;
			this.hdrStatus.Name = "hdrStatus";
			this.hdrStatus.ReadOnly = true;
			this.hdrStatus.Width = 175;
			// 
			// hdrLaunchIcon
			// 
			this.hdrLaunchIcon.HeaderText = "";
			this.hdrLaunchIcon.MinimumWidth = 9;
			this.hdrLaunchIcon.Name = "hdrLaunchIcon";
			this.hdrLaunchIcon.ReadOnly = true;
			this.hdrLaunchIcon.Width = 24;
			// 
			// hdrKillIcon
			// 
			this.hdrKillIcon.HeaderText = "";
			this.hdrKillIcon.MinimumWidth = 9;
			this.hdrKillIcon.Name = "hdrKillIcon";
			this.hdrKillIcon.ReadOnly = true;
			this.hdrKillIcon.Width = 24;
			// 
			// hdrRestartIcon
			// 
			this.hdrRestartIcon.HeaderText = "";
			this.hdrRestartIcon.MinimumWidth = 9;
			this.hdrRestartIcon.Name = "hdrRestartIcon";
			this.hdrRestartIcon.ReadOnly = true;
			this.hdrRestartIcon.Width = 24;
			// 
			// hdrEnabled
			// 
			this.hdrEnabled.HeaderText = "Enabled";
			this.hdrEnabled.MinimumWidth = 9;
			this.hdrEnabled.Name = "hdrEnabled";
			this.hdrEnabled.ReadOnly = true;
			this.hdrEnabled.Width = 50;
			// 
			// hdrPlan
			// 
			this.hdrPlan.HeaderText = "Last Plan";
			this.hdrPlan.MinimumWidth = 9;
			this.hdrPlan.Name = "hdrPlan";
			this.hdrPlan.ReadOnly = true;
			this.hdrPlan.Width = 175;
			// 
			// toolStripApps
			// 
			this.toolStripApps.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.toolStripApps.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnSelectPlan,
            this.btnStartPlan,
            this.btnStopPlan,
            this.btnKillPlan,
            this.btnRestartPlan,
            this.bntKillAll2,
            this.btnShowJustAppsFromCurrentPlan});
			this.toolStripApps.Location = new System.Drawing.Point(4, 5);
			this.toolStripApps.Name = "toolStripApps";
			this.toolStripApps.Padding = new System.Windows.Forms.Padding(0, 0, 3, 0);
			this.toolStripApps.Size = new System.Drawing.Size(1051, 25);
			this.toolStripApps.TabIndex = 4;
			this.toolStripApps.Text = "toolStrip1";
			// 
			// btnSelectPlan
			// 
			this.btnSelectPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnSelectPlan.Image = global::Dirigent.Gui.WinForms.Resource1.open;
			this.btnSelectPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnSelectPlan.Name = "btnSelectPlan";
			this.btnSelectPlan.Size = new System.Drawing.Size(40, 19);
			this.btnSelectPlan.Text = "Select Plan";
			this.btnSelectPlan.Click += new System.EventHandler(this.selectPlanMenuItem_Click);
			// 
			// btnStartPlan
			// 
			this.btnStartPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnStartPlan.Image = global::Dirigent.Gui.WinForms.Resource1.play;
			this.btnStartPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnStartPlan.Name = "btnStartPlan";
			this.btnStartPlan.Size = new System.Drawing.Size(40, 19);
			this.btnStartPlan.Text = "Start Plan";
			this.btnStartPlan.Click += new System.EventHandler(this.startPlanMenuItem_Click);
			// 
			// btnStopPlan
			// 
			this.btnStopPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnStopPlan.Image = global::Dirigent.Gui.WinForms.Resource1.stop;
			this.btnStopPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnStopPlan.Name = "btnStopPlan";
			this.btnStopPlan.Size = new System.Drawing.Size(40, 19);
			this.btnStopPlan.Text = "Stop Plan";
			this.btnStopPlan.Click += new System.EventHandler(this.stopPlanMenuItem_Click);
			// 
			// btnKillPlan
			// 
			this.btnKillPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnStopPlan.Image = global::Dirigent.Gui.WinForms.Resource1.delete;
			this.btnKillPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnKillPlan.Name = "btnKillPlan";
			this.btnKillPlan.Size = new System.Drawing.Size(40, 19);
			this.btnKillPlan.Text = "Kill Plan";
			this.btnKillPlan.Click += new System.EventHandler(this.killPlanMenuItem_Click);
			// 
			// btnRestartPlan
			// 
			this.btnRestartPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnStopPlan.Image = global::Dirigent.Gui.WinForms.Resource1.refresh;
			this.btnRestartPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnRestartPlan.Name = "btnRestartPlan";
			this.btnRestartPlan.Size = new System.Drawing.Size(40, 19);
			this.btnRestartPlan.Text = "Restart Plan";
			this.btnRestartPlan.Click += new System.EventHandler(this.restartPlanMenuItem_Click);
			// 
			// bntKillAll2
			// 
			this.bntKillAll2.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnStopPlan.Image = global::Dirigent.Gui.WinForms.Resource1.killall;
			this.bntKillAll2.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.bntKillAll2.Name = "bntKillAll2";
			this.bntKillAll2.Size = new System.Drawing.Size(40, 19);
			this.bntKillAll2.Text = "Kill All";
			this.bntKillAll2.Click += new System.EventHandler(this.bntKillAll2_Click);
			// 
			// btnShowJustAppsFromCurrentPlan
			// 
			this.btnShowJustAppsFromCurrentPlan.CheckOnClick = true;
			this.btnStopPlan.Image = global::Dirigent.Gui.WinForms.Resource1.items_few;
			this.btnShowJustAppsFromCurrentPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnShowJustAppsFromCurrentPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnShowJustAppsFromCurrentPlan.Name = "btnShowJustAppsFromCurrentPlan";
			this.btnShowJustAppsFromCurrentPlan.Size = new System.Drawing.Size(40, 19);
			this.btnShowJustAppsFromCurrentPlan.Text = "Show just apps from the current plan";
			// 
			// tabPagePlans
			// 
			this.tabPagePlans.Controls.Add(this.gridPlans);
			this.tabPagePlans.Controls.Add(this.toolStripPlans);
			this.tabPagePlans.Location = new System.Drawing.Point(4, 39);
			this.tabPagePlans.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPagePlans.Name = "tabPagePlans";
			this.tabPagePlans.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPagePlans.Size = new System.Drawing.Size(1059, 498);
			this.tabPagePlans.TabIndex = 1;
			this.tabPagePlans.Text = "Plans";
			this.tabPagePlans.UseVisualStyleBackColor = true;
			// 
			// gridPlans
			// 
			this.gridPlans.AllowUserToAddRows = false;
			this.gridPlans.AllowUserToDeleteRows = false;
			this.gridPlans.BackgroundColor = System.Drawing.SystemColors.Window;
			this.gridPlans.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.gridPlans.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.hdrPlanName,
            this.Status,
            this.hdrPlanStart,
            this.hdrPlanStop,
            this.hdrPlanKill,
            this.hdrPlanRestart});
			this.gridPlans.Dock = System.Windows.Forms.DockStyle.Fill;
			this.gridPlans.Location = new System.Drawing.Point(4, 30);
			this.gridPlans.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.gridPlans.MultiSelect = false;
			this.gridPlans.Name = "gridPlans";
			this.gridPlans.ReadOnly = true;
			this.gridPlans.RowHeadersVisible = false;
			this.gridPlans.RowHeadersWidth = 72;
			this.gridPlans.RowTemplate.Height = 24;
			this.gridPlans.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.gridPlans.Size = new System.Drawing.Size(1051, 463);
			this.gridPlans.TabIndex = 6;
			this.gridPlans.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.gridPlans_CellFormatting);
			this.gridPlans.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gridPlans_MouseClick);
			this.gridPlans.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.gridPlans_MouseDoubleClick);
			// 
			// hdrPlanName
			// 
			this.hdrPlanName.HeaderText = "Plan Name";
			this.hdrPlanName.MinimumWidth = 9;
			this.hdrPlanName.Name = "hdrPlanName";
			this.hdrPlanName.ReadOnly = true;
			this.hdrPlanName.Width = 250;
			// 
			// Status
			// 
			this.Status.HeaderText = "Status";
			this.Status.MinimumWidth = 9;
			this.Status.Name = "Status";
			this.Status.ReadOnly = true;
			this.Status.Width = 175;
			// 
			// hdrPlanStart
			// 
			this.hdrPlanStart.HeaderText = "";
			this.hdrPlanStart.MinimumWidth = 9;
			this.hdrPlanStart.Name = "hdrPlanStart";
			this.hdrPlanStart.ReadOnly = true;
			this.hdrPlanStart.Width = 24;
			// 
			// hdrPlanStop
			// 
			this.hdrPlanStop.HeaderText = "";
			this.hdrPlanStop.MinimumWidth = 9;
			this.hdrPlanStop.Name = "hdrPlanStop";
			this.hdrPlanStop.ReadOnly = true;
			this.hdrPlanStop.Width = 24;
			// 
			// hdrPlanKill
			// 
			this.hdrPlanKill.HeaderText = "";
			this.hdrPlanKill.MinimumWidth = 9;
			this.hdrPlanKill.Name = "hdrPlanKill";
			this.hdrPlanKill.ReadOnly = true;
			this.hdrPlanKill.Width = 24;
			// 
			// hdrPlanRestart
			// 
			this.hdrPlanRestart.HeaderText = "";
			this.hdrPlanRestart.MinimumWidth = 9;
			this.hdrPlanRestart.Name = "hdrPlanRestart";
			this.hdrPlanRestart.ReadOnly = true;
			this.hdrPlanRestart.Width = 24;
			// 
			// toolStripPlans
			// 
			this.toolStripPlans.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.toolStripPlans.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnKillAll});
			this.toolStripPlans.Location = new System.Drawing.Point(4, 5);
			this.toolStripPlans.Name = "toolStripPlans";
			this.toolStripPlans.Size = new System.Drawing.Size(1051, 25);
			this.toolStripPlans.TabIndex = 1;
			this.toolStripPlans.Text = "myToolStrip1";
			// 
			// btnKillAll
			// 
			this.btnKillAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnKillAll.Image = global::Dirigent.Gui.WinForms.Resource1.killall;
			this.btnKillAll.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnKillAll.Name = "btnKillAll";
			this.btnKillAll.Size = new System.Drawing.Size(40, 19);
			this.btnKillAll.Text = "Kill All";
			this.btnKillAll.Click += new System.EventHandler(this.btnKillAll_Click);
			// 
			// frmMain
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 30F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1067, 619);
			this.Controls.Add(this.tabControl1);
			this.Controls.Add(this.statusStrip);
			this.Controls.Add(this.menuMain);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MainMenuStrip = this.menuMain;
			this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.Name = "frmMain";
			this.Text = "Dirigent";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.frmMain_FormClosed);
			this.Resize += new System.EventHandler(this.frmMain_Resize);
			this.statusStrip.ResumeLayout(false);
			this.statusStrip.PerformLayout();
			this.menuMain.ResumeLayout(false);
			this.menuMain.PerformLayout();
			this.tabControl1.ResumeLayout(false);
			this.tabPageApps.ResumeLayout(false);
			this.tabPageApps.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridApps)).EndInit();
			this.toolStripApps.ResumeLayout(false);
			this.toolStripApps.PerformLayout();
			this.tabPagePlans.ResumeLayout(false);
			this.tabPagePlans.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridPlans)).EndInit();
			this.toolStripPlans.ResumeLayout(false);
			this.toolStripPlans.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.StatusStrip statusStrip;
		private System.Windows.Forms.MenuStrip menuMain;
		private System.Windows.Forms.ToolStripMenuItem planToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem startPlanToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem killPlanToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem restartPlanToolStripMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem selectPlanToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
		private System.Windows.Forms.Timer tmrTick;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
		private System.Windows.Forms.ToolStripMenuItem stopPlanToolStripMenuItem;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage tabPageApps;
		private MyToolStrip toolStripApps;
		private System.Windows.Forms.ToolStripButton btnSelectPlan;
		private System.Windows.Forms.ToolStripButton btnStartPlan;
		private System.Windows.Forms.ToolStripButton btnStopPlan;
		private System.Windows.Forms.ToolStripButton btnKillPlan;
		private System.Windows.Forms.ToolStripButton btnRestartPlan;
		private System.Windows.Forms.ToolStripButton btnShowJustAppsFromCurrentPlan;
		private System.Windows.Forms.TabPage tabPagePlans;
		private System.Windows.Forms.DataGridView gridApps;
		private System.Windows.Forms.DataGridView gridPlans;
		private System.Windows.Forms.DataGridViewTextBoxColumn hdrPlanName;
		private System.Windows.Forms.DataGridViewTextBoxColumn Status;
		private System.Windows.Forms.DataGridViewImageColumn hdrPlanStart;
		private System.Windows.Forms.DataGridViewImageColumn hdrPlanStop;
		private System.Windows.Forms.DataGridViewImageColumn hdrPlanKill;
		private System.Windows.Forms.DataGridViewImageColumn hdrPlanRestart;
		private System.Windows.Forms.ToolStripMenuItem onlineDocumentationToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem reloadToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem reloadSharedConfigToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem exitAndKillAppsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem exitAndLeaveAppsRunningToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem killToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem killAllRunningAppsToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem powerToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem rebootAllToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem shutdownAllToolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem reinstallToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem reinstallManuallyToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem1;
		private MyToolStrip toolStripPlans;
		private System.Windows.Forms.ToolStripButton btnKillAll;
		private System.Windows.Forms.ToolStripButton bntKillAll2;
		private System.Windows.Forms.DataGridViewTextBoxColumn hdrName;
		private System.Windows.Forms.DataGridViewTextBoxColumn hdrStatus;
		private System.Windows.Forms.DataGridViewImageColumn hdrLaunchIcon;
		private System.Windows.Forms.DataGridViewImageColumn hdrKillIcon;
		private System.Windows.Forms.DataGridViewImageColumn hdrRestartIcon;
		private System.Windows.Forms.DataGridViewCheckBoxColumn hdrEnabled;
		private System.Windows.Forms.DataGridViewTextBoxColumn hdrPlan;
	}
}

