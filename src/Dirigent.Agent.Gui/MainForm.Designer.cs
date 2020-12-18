namespace Dirigent.Agent.Gui
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
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
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
			this.toolStrip1 = new Dirigent.Agent.Gui.MyToolStrip();
			this.btnSelectPlan = new System.Windows.Forms.ToolStripButton();
			this.btnStartPlan = new System.Windows.Forms.ToolStripButton();
			this.btnStopPlan = new System.Windows.Forms.ToolStripButton();
			this.btnKillPlan = new System.Windows.Forms.ToolStripButton();
			this.btnRestartPlan = new System.Windows.Forms.ToolStripButton();
			this.tabPagePlans = new System.Windows.Forms.TabPage();
			this.gridPlans = new System.Windows.Forms.DataGridView();
			this.hdrPlanName = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.Status = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.hdrPlanStart = new System.Windows.Forms.DataGridViewImageColumn();
			this.hdrPlanStop = new System.Windows.Forms.DataGridViewImageColumn();
			this.hdrPlanKill = new System.Windows.Forms.DataGridViewImageColumn();
			this.hdrPlanRestart = new System.Windows.Forms.DataGridViewImageColumn();
			this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.exitToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.statusStrip.SuspendLayout();
			this.menuMain.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPageApps.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridApps)).BeginInit();
			this.toolStrip1.SuspendLayout();
			this.tabPagePlans.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridPlans)).BeginInit();
			this.SuspendLayout();
			// 
			// statusStrip
			// 
			this.statusStrip.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1});
			this.statusStrip.Location = new System.Drawing.Point(0, 308);
			this.statusStrip.Name = "statusStrip";
			this.statusStrip.Size = new System.Drawing.Size(711, 22);
			this.statusStrip.TabIndex = 0;
			this.statusStrip.Text = "statusStrip1";
			// 
			// toolStripStatusLabel1
			// 
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			this.toolStripStatusLabel1.Size = new System.Drawing.Size(0, 17);
			// 
			// menuMain
			// 
			this.menuMain.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.menuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.toolsToolStripMenuItem,
            this.helpToolStripMenuItem,
            this.planToolStripMenuItem});
			this.menuMain.Location = new System.Drawing.Point(0, 0);
			this.menuMain.Name = "menuMain";
			this.menuMain.Size = new System.Drawing.Size(711, 28);
			this.menuMain.TabIndex = 1;
			this.menuMain.Text = "menuStrip1";
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
			this.planToolStripMenuItem.Size = new System.Drawing.Size(49, 24);
			this.planToolStripMenuItem.Text = "Plan";
			// 
			// startPlanToolStripMenuItem
			// 
			this.startPlanToolStripMenuItem.Name = "startPlanToolStripMenuItem";
			this.startPlanToolStripMenuItem.Size = new System.Drawing.Size(130, 26);
			this.startPlanToolStripMenuItem.Text = "Start";
			this.startPlanToolStripMenuItem.Click += new System.EventHandler(this.startPlanMenuItem_Click);
			// 
			// stopPlanToolStripMenuItem
			// 
			this.stopPlanToolStripMenuItem.Name = "stopPlanToolStripMenuItem";
			this.stopPlanToolStripMenuItem.Size = new System.Drawing.Size(130, 26);
			this.stopPlanToolStripMenuItem.Text = "Stop";
			this.stopPlanToolStripMenuItem.Click += new System.EventHandler(this.stopPlanMenuItem_Click);
			// 
			// restartPlanToolStripMenuItem
			// 
			this.restartPlanToolStripMenuItem.Name = "restartPlanToolStripMenuItem";
			this.restartPlanToolStripMenuItem.Size = new System.Drawing.Size(130, 26);
			this.restartPlanToolStripMenuItem.Text = "Restart";
			this.restartPlanToolStripMenuItem.Click += new System.EventHandler(this.restartPlanMenuItem_Click);
			// 
			// killPlanToolStripMenuItem
			// 
			this.killPlanToolStripMenuItem.Name = "killPlanToolStripMenuItem";
			this.killPlanToolStripMenuItem.Size = new System.Drawing.Size(130, 26);
			this.killPlanToolStripMenuItem.Text = "Kill";
			this.killPlanToolStripMenuItem.Click += new System.EventHandler(this.killPlanMenuItem_Click);
			// 
			// toolStripMenuItem1
			// 
			this.toolStripMenuItem1.Name = "toolStripMenuItem1";
			this.toolStripMenuItem1.Size = new System.Drawing.Size(127, 6);
			// 
			// selectPlanToolStripMenuItem
			// 
			this.selectPlanToolStripMenuItem.Name = "selectPlanToolStripMenuItem";
			this.selectPlanToolStripMenuItem.Size = new System.Drawing.Size(130, 26);
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
			this.reloadToolStripMenuItem.Size = new System.Drawing.Size(315, 40);
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
			this.killToolStripMenuItem1.Size = new System.Drawing.Size(315, 40);
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
			this.powerToolStripMenuItem1.Size = new System.Drawing.Size(315, 40);
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
			this.helpToolStripMenuItem.Size = new System.Drawing.Size(53, 24);
			this.helpToolStripMenuItem.Text = "Help";
			// 
			// aboutToolStripMenuItem
			// 
			this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
			this.aboutToolStripMenuItem.Size = new System.Drawing.Size(234, 26);
			this.aboutToolStripMenuItem.Text = "&About";
			this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutMenuItem_Click);
			// 
			// onlineDocumentationToolStripMenuItem
			// 
			this.onlineDocumentationToolStripMenuItem.Name = "onlineDocumentationToolStripMenuItem";
			this.onlineDocumentationToolStripMenuItem.Size = new System.Drawing.Size(234, 26);
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
			this.tabControl1.Location = new System.Drawing.Point(0, 28);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(711, 280);
			this.tabControl1.TabIndex = 4;
			// 
			// tabPageApps
			// 
			this.tabPageApps.Controls.Add(this.gridApps);
			this.tabPageApps.Controls.Add(this.toolStrip1);
			this.tabPageApps.Location = new System.Drawing.Point(4, 25);
			this.tabPageApps.Name = "tabPageApps";
			this.tabPageApps.Padding = new System.Windows.Forms.Padding(3);
			this.tabPageApps.Size = new System.Drawing.Size(703, 251);
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
            this.hdrEnabled});
			this.gridApps.Dock = System.Windows.Forms.DockStyle.Fill;
			this.gridApps.Location = new System.Drawing.Point(3, 38);
			this.gridApps.MultiSelect = false;
			this.gridApps.Name = "gridApps";
			this.gridApps.ReadOnly = true;
			this.gridApps.RowHeadersVisible = false;
			this.gridApps.RowTemplate.Height = 24;
			this.gridApps.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.gridApps.Size = new System.Drawing.Size(697, 210);
			this.gridApps.TabIndex = 6;
			this.gridApps.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gridApps_MouseClick);
			this.gridApps.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.gridApps_MouseDoubleClick);
			// 
			// hdrName
			// 
			this.hdrName.HeaderText = "Application Name";
			this.hdrName.Name = "hdrName";
			this.hdrName.ReadOnly = true;
			this.hdrName.Width = 250;
			// 
			// hdrStatus
			// 
			this.hdrStatus.HeaderText = "Status";
			this.hdrStatus.Name = "hdrStatus";
			this.hdrStatus.ReadOnly = true;
			// 
			// hdrLaunchIcon
			// 
			this.hdrLaunchIcon.HeaderText = "";
			this.hdrLaunchIcon.Name = "hdrLaunchIcon";
			this.hdrLaunchIcon.ReadOnly = true;
			this.hdrLaunchIcon.Width = 24;
			// 
			// hdrKillIcon
			// 
			this.hdrKillIcon.HeaderText = "";
			this.hdrKillIcon.Name = "hdrKillIcon";
			this.hdrKillIcon.ReadOnly = true;
			this.hdrKillIcon.Width = 24;
			// 
			// hdrRestartIcon
			// 
			this.hdrRestartIcon.HeaderText = "";
			this.hdrRestartIcon.Name = "hdrRestartIcon";
			this.hdrRestartIcon.ReadOnly = true;
			this.hdrRestartIcon.Width = 24;
			// 
			// hdrEnabled
			// 
			this.hdrEnabled.HeaderText = "Enabled";
			this.hdrEnabled.Name = "hdrEnabled";
			this.hdrEnabled.ReadOnly = true;
			this.hdrEnabled.Width = 50;
			// 
			// toolStrip1
			// 
			this.toolStrip1.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnSelectPlan,
            this.btnStartPlan,
            this.btnStopPlan,
            this.btnKillPlan,
            this.btnRestartPlan});
			this.toolStrip1.Location = new System.Drawing.Point(3, 3);
			this.toolStrip1.Name = "toolStrip1";
			this.toolStrip1.Size = new System.Drawing.Size(697, 35);
			this.toolStrip1.TabIndex = 4;
			this.toolStrip1.Text = "toolStrip1";
			// 
			// btnSelectPlan
			// 
			this.btnSelectPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnSelectPlan.Image = global::Dirigent.Agent.Gui.Resource1.open;
			this.btnSelectPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnSelectPlan.Name = "btnSelectPlan";
			this.btnSelectPlan.Size = new System.Drawing.Size(32, 32);
			this.btnSelectPlan.Text = "Select Plan";
			this.btnSelectPlan.Click += new System.EventHandler(this.selectPlanMenuItem_Click);
			// 
			// btnStartPlan
			// 
			this.btnStartPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnStartPlan.Image = global::Dirigent.Agent.Gui.Resource1.play;
			this.btnStartPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnStartPlan.Name = "btnStartPlan";
			this.btnStartPlan.Size = new System.Drawing.Size(32, 32);
			this.btnStartPlan.Text = "Start Plan";
			this.btnStartPlan.Click += new System.EventHandler(this.startPlanMenuItem_Click);
			// 
			// btnStopPlan
			// 
			this.btnStopPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnStopPlan.Image = global::Dirigent.Agent.Gui.Resource1.stop;
			this.btnStopPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnStopPlan.Name = "btnStopPlan";
			this.btnStopPlan.Size = new System.Drawing.Size(32, 32);
			this.btnStopPlan.Text = "Stop Plan";
			this.btnStopPlan.Click += new System.EventHandler(this.stopPlanMenuItem_Click);
			// 
			// btnKillPlan
			// 
			this.btnKillPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnKillPlan.Image = global::Dirigent.Agent.Gui.Resource1.delete;
			this.btnKillPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnKillPlan.Name = "btnKillPlan";
			this.btnKillPlan.Size = new System.Drawing.Size(32, 32);
			this.btnKillPlan.Text = "Kill Plan";
			this.btnKillPlan.Click += new System.EventHandler(this.killPlanMenuItem_Click);
			// 
			// btnRestartPlan
			// 
			this.btnRestartPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnRestartPlan.Image = global::Dirigent.Agent.Gui.Resource1.refresh;
			this.btnRestartPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnRestartPlan.Name = "btnRestartPlan";
			this.btnRestartPlan.Size = new System.Drawing.Size(32, 32);
			this.btnRestartPlan.Text = "Restart Plan";
			this.btnRestartPlan.Click += new System.EventHandler(this.restartPlanMenuItem_Click);
			// 
			// tabPagePlans
			// 
			this.tabPagePlans.Controls.Add(this.gridPlans);
			this.tabPagePlans.Location = new System.Drawing.Point(4, 25);
			this.tabPagePlans.Name = "tabPagePlans";
			this.tabPagePlans.Padding = new System.Windows.Forms.Padding(3);
			this.tabPagePlans.Size = new System.Drawing.Size(577, 255);
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
			this.gridPlans.Location = new System.Drawing.Point(3, 3);
			this.gridPlans.MultiSelect = false;
			this.gridPlans.Name = "gridPlans";
			this.gridPlans.ReadOnly = true;
			this.gridPlans.RowHeadersVisible = false;
			this.gridPlans.RowTemplate.Height = 24;
			this.gridPlans.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.gridPlans.Size = new System.Drawing.Size(571, 249);
			this.gridPlans.TabIndex = 0;
			this.gridPlans.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gridPlans_MouseClick);
			this.gridPlans.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.gridPlans_MouseDoubleClick);
			// 
			// hdrPlanName
			// 
			this.hdrPlanName.HeaderText = "Plan Name";
			this.hdrPlanName.Name = "hdrPlanName";
			this.hdrPlanName.ReadOnly = true;
			this.hdrPlanName.Width = 250;
			// 
			// Status
			// 
			this.Status.HeaderText = "Status";
			this.Status.Name = "Status";
			this.Status.ReadOnly = true;
			// 
			// hdrPlanStart
			// 
			this.hdrPlanStart.HeaderText = "";
			this.hdrPlanStart.Name = "hdrPlanStart";
			this.hdrPlanStart.ReadOnly = true;
			this.hdrPlanStart.Width = 24;
			// 
			// hdrPlanStop
			// 
			this.hdrPlanStop.HeaderText = "";
			this.hdrPlanStop.Name = "hdrPlanStop";
			this.hdrPlanStop.ReadOnly = true;
			this.hdrPlanStop.Width = 24;
			// 
			// hdrPlanKill
			// 
			this.hdrPlanKill.HeaderText = "";
			this.hdrPlanKill.Name = "hdrPlanKill";
			this.hdrPlanKill.ReadOnly = true;
			this.hdrPlanKill.Width = 24;
			// 
			// hdrPlanRestart
			// 
			this.hdrPlanRestart.HeaderText = "";
			this.hdrPlanRestart.Name = "hdrPlanRestart";
			this.hdrPlanRestart.ReadOnly = true;
			this.hdrPlanRestart.Width = 24;
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
			this.exitToolStripMenuItem1.Size = new System.Drawing.Size(315, 40);
			this.exitToolStripMenuItem1.Text = "Exit";
			this.exitToolStripMenuItem1.Click += new System.EventHandler(this.exitToolStripMenuItem1_Click);
			// 
			// frmMain
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(711, 330);
			this.Controls.Add(this.tabControl1);
			this.Controls.Add(this.statusStrip);
			this.Controls.Add(this.menuMain);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MainMenuStrip = this.menuMain;
			this.Name = "frmMain";
			this.Text = "Dirigent";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
			this.Resize += new System.EventHandler(this.frmMain_Resize);
			this.statusStrip.ResumeLayout(false);
			this.statusStrip.PerformLayout();
			this.menuMain.ResumeLayout(false);
			this.menuMain.PerformLayout();
			this.tabControl1.ResumeLayout(false);
			this.tabPageApps.ResumeLayout(false);
			this.tabPageApps.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridApps)).EndInit();
			this.toolStrip1.ResumeLayout(false);
			this.toolStrip1.PerformLayout();
			this.tabPagePlans.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.gridPlans)).EndInit();
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
        private Dirigent.Agent.Gui.MyToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnSelectPlan;
        private System.Windows.Forms.ToolStripButton btnStartPlan;
        private System.Windows.Forms.ToolStripButton btnStopPlan;
        private System.Windows.Forms.ToolStripButton btnKillPlan;
        private System.Windows.Forms.ToolStripButton btnRestartPlan;
        private System.Windows.Forms.TabPage tabPagePlans;
        private System.Windows.Forms.DataGridView gridApps;
        private System.Windows.Forms.DataGridView gridPlans;
		private System.Windows.Forms.DataGridViewTextBoxColumn hdrPlanName;
		private System.Windows.Forms.DataGridViewTextBoxColumn Status;
		private System.Windows.Forms.DataGridViewImageColumn hdrPlanStart;
		private System.Windows.Forms.DataGridViewImageColumn hdrPlanStop;
		private System.Windows.Forms.DataGridViewImageColumn hdrPlanKill;
		private System.Windows.Forms.DataGridViewImageColumn hdrPlanRestart;
        private System.Windows.Forms.DataGridViewTextBoxColumn hdrName;
        private System.Windows.Forms.DataGridViewTextBoxColumn hdrStatus;
        private System.Windows.Forms.DataGridViewImageColumn hdrLaunchIcon;
        private System.Windows.Forms.DataGridViewImageColumn hdrKillIcon;
        private System.Windows.Forms.DataGridViewImageColumn hdrRestartIcon;
        private System.Windows.Forms.DataGridViewCheckBoxColumn hdrEnabled;
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
	}
}

