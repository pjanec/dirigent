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
			this.tmrTick = new System.Windows.Forms.Timer(this.components);
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPageApps = new System.Windows.Forms.TabPage();
			this.gridApps = new Zuby.ADGV.AdvancedDataGridView();
			this.toolStripApps = new Dirigent.Gui.WinForms.MyToolStrip();
			this.btnSelectPlan = new System.Windows.Forms.ToolStripButton();
			this.btnStartPlan = new System.Windows.Forms.ToolStripButton();
			this.btnStopPlan = new System.Windows.Forms.ToolStripButton();
			this.btnKillPlan = new System.Windows.Forms.ToolStripButton();
			this.btnRestartPlan = new System.Windows.Forms.ToolStripButton();
			this.bntAppsKillAll = new System.Windows.Forms.ToolStripButton();
			this.btnShowJustAppsFromCurrentPlan = new System.Windows.Forms.ToolStripButton();
			this.tabPagePlans = new System.Windows.Forms.TabPage();
			this.gridPlans = new Zuby.ADGV.AdvancedDataGridView();
			this.toolStripPlans = new Dirigent.Gui.WinForms.MyToolStrip();
			this.btnPlansKillAll = new System.Windows.Forms.ToolStripButton();
			this.tabPageScripts = new System.Windows.Forms.TabPage();
			this.gridScripts = new Zuby.ADGV.AdvancedDataGridView();
			this.toolStripScripts = new Dirigent.Gui.WinForms.MyToolStrip();
			this.btnScriptsKillAll = new System.Windows.Forms.ToolStripButton();
			this.tabPageMachs = new System.Windows.Forms.TabPage();
			this.gridMachs = new Zuby.ADGV.AdvancedDataGridView();
			this.toolStripMachs = new Dirigent.Gui.WinForms.MyToolStrip();
			this.btnMachsKillAll = new System.Windows.Forms.ToolStripButton();
			this.tabPageFiles = new System.Windows.Forms.TabPage();
			this.gridFiles = new Zuby.ADGV.AdvancedDataGridView();
			this.toolStripFiles = new Dirigent.Gui.WinForms.MyToolStrip();
			this.btnFilesKillAll = new System.Windows.Forms.ToolStripButton();
			this.stblbMain = new System.Windows.Forms.ToolStripStatusLabel();
			this.stblbSSH = new System.Windows.Forms.ToolStripStatusLabel();
			this.statusStrip.SuspendLayout();
			this.menuMain.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPageApps.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridApps)).BeginInit();
			this.toolStripApps.SuspendLayout();
			this.tabPagePlans.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridPlans)).BeginInit();
			this.toolStripPlans.SuspendLayout();
			this.tabPageScripts.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridScripts)).BeginInit();
			this.toolStripScripts.SuspendLayout();
			this.tabPageMachs.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridMachs)).BeginInit();
			this.toolStripMachs.SuspendLayout();
			this.tabPageFiles.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridFiles)).BeginInit();
			this.toolStripFiles.SuspendLayout();
			this.SuspendLayout();
			// 
			// statusStrip
			// 
			this.statusStrip.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.stblbMain,
            this.stblbSSH});
			this.statusStrip.Location = new System.Drawing.Point(0, 484);
			this.statusStrip.Name = "statusStrip";
			this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 18, 0);
			this.statusStrip.Size = new System.Drawing.Size(889, 32);
			this.statusStrip.TabIndex = 0;
			this.statusStrip.Text = "statusStrip1";
			// 
			// menuMain
			// 
			this.menuMain.ImageScalingSize = new System.Drawing.Size(24, 24);
			this.menuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.planToolStripMenuItem});
			this.menuMain.Location = new System.Drawing.Point(0, 0);
			this.menuMain.Name = "menuMain";
			this.menuMain.Padding = new System.Windows.Forms.Padding(8, 3, 0, 3);
			this.menuMain.Size = new System.Drawing.Size(889, 35);
			this.menuMain.TabIndex = 1;
			this.menuMain.Text = "menuStrip1";
			// 
			// fileToolStripMenuItem
			// 
			this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem1});
			this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
			this.fileToolStripMenuItem.Size = new System.Drawing.Size(54, 29);
			this.fileToolStripMenuItem.Text = "File";
			// 
			// exitToolStripMenuItem1
			// 
			this.exitToolStripMenuItem1.Name = "exitToolStripMenuItem1";
			this.exitToolStripMenuItem1.Size = new System.Drawing.Size(141, 34);
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
			this.planToolStripMenuItem.Size = new System.Drawing.Size(61, 29);
			this.planToolStripMenuItem.Text = "Plan";
			// 
			// startPlanToolStripMenuItem
			// 
			this.startPlanToolStripMenuItem.Name = "startPlanToolStripMenuItem";
			this.startPlanToolStripMenuItem.Size = new System.Drawing.Size(316, 34);
			this.startPlanToolStripMenuItem.Text = "Start";
			this.startPlanToolStripMenuItem.Click += new System.EventHandler(this.startPlanMenuItem_Click);
			// 
			// stopPlanToolStripMenuItem
			// 
			this.stopPlanToolStripMenuItem.Name = "stopPlanToolStripMenuItem";
			this.stopPlanToolStripMenuItem.Size = new System.Drawing.Size(316, 34);
			this.stopPlanToolStripMenuItem.Text = "Stop (leave apps running)";
			this.stopPlanToolStripMenuItem.Click += new System.EventHandler(this.stopPlanMenuItem_Click);
			// 
			// restartPlanToolStripMenuItem
			// 
			this.restartPlanToolStripMenuItem.Name = "restartPlanToolStripMenuItem";
			this.restartPlanToolStripMenuItem.Size = new System.Drawing.Size(316, 34);
			this.restartPlanToolStripMenuItem.Text = "Restart";
			this.restartPlanToolStripMenuItem.Click += new System.EventHandler(this.restartPlanMenuItem_Click);
			// 
			// killPlanToolStripMenuItem
			// 
			this.killPlanToolStripMenuItem.Name = "killPlanToolStripMenuItem";
			this.killPlanToolStripMenuItem.Size = new System.Drawing.Size(316, 34);
			this.killPlanToolStripMenuItem.Text = "Kill apps";
			this.killPlanToolStripMenuItem.Click += new System.EventHandler(this.killPlanMenuItem_Click);
			// 
			// toolStripMenuItem1
			// 
			this.toolStripMenuItem1.Name = "toolStripMenuItem1";
			this.toolStripMenuItem1.Size = new System.Drawing.Size(313, 6);
			// 
			// selectPlanToolStripMenuItem
			// 
			this.selectPlanToolStripMenuItem.Name = "selectPlanToolStripMenuItem";
			this.selectPlanToolStripMenuItem.Size = new System.Drawing.Size(316, 34);
			this.selectPlanToolStripMenuItem.Text = "Select";
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
			this.tabControl1.Controls.Add(this.tabPageScripts);
			this.tabControl1.Controls.Add(this.tabPageMachs);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 35);
			this.tabControl1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(889, 449);
			this.tabControl1.TabIndex = 4;
			// 
			// tabPageApps
			// 
			this.tabPageApps.Controls.Add(this.gridApps);
			this.tabPageApps.Controls.Add(this.toolStripApps);
			this.tabPageApps.Location = new System.Drawing.Point(4, 34);
			this.tabPageApps.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPageApps.Name = "tabPageApps";
			this.tabPageApps.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPageApps.Size = new System.Drawing.Size(881, 411);
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
			this.gridApps.Dock = System.Windows.Forms.DockStyle.Fill;
			this.gridApps.FilterAndSortEnabled = true;
			this.gridApps.FilterStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridApps.Location = new System.Drawing.Point(3, 41);
			this.gridApps.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.gridApps.MultiSelect = false;
			this.gridApps.Name = "gridApps";
			this.gridApps.ReadOnly = true;
			this.gridApps.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.gridApps.RowHeadersVisible = false;
			this.gridApps.RowHeadersWidth = 72;
			this.gridApps.RowTemplate.Height = 24;
			this.gridApps.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.gridApps.Size = new System.Drawing.Size(875, 366);
			this.gridApps.SortStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridApps.TabIndex = 6;
			this.gridApps.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.gridApps_CellFormatting);
			this.gridApps.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gridApps_MouseClick);
			this.gridApps.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.gridApps_MouseDoubleClick);
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
            this.bntAppsKillAll,
            this.btnShowJustAppsFromCurrentPlan});
			this.toolStripApps.Location = new System.Drawing.Point(3, 4);
			this.toolStripApps.Name = "toolStripApps";
			this.toolStripApps.Size = new System.Drawing.Size(875, 37);
			this.toolStripApps.TabIndex = 4;
			this.toolStripApps.Text = "toolStrip1";
			// 
			// btnSelectPlan
			// 
			this.btnSelectPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnSelectPlan.Image = ((System.Drawing.Image)(resources.GetObject("btnSelectPlan.Image")));
			this.btnSelectPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnSelectPlan.Name = "btnSelectPlan";
			this.btnSelectPlan.Size = new System.Drawing.Size(34, 32);
			this.btnSelectPlan.Text = "Select Plan";
			this.btnSelectPlan.Click += new System.EventHandler(this.selectPlanMenuItem_Click);
			// 
			// btnStartPlan
			// 
			this.btnStartPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnStartPlan.Image = ((System.Drawing.Image)(resources.GetObject("btnStartPlan.Image")));
			this.btnStartPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnStartPlan.Name = "btnStartPlan";
			this.btnStartPlan.Size = new System.Drawing.Size(34, 32);
			this.btnStartPlan.Text = "Start Plan";
			this.btnStartPlan.Click += new System.EventHandler(this.startPlanMenuItem_Click);
			// 
			// btnStopPlan
			// 
			this.btnStopPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnStopPlan.Image = ((System.Drawing.Image)(resources.GetObject("btnStopPlan.Image")));
			this.btnStopPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnStopPlan.Name = "btnStopPlan";
			this.btnStopPlan.Size = new System.Drawing.Size(34, 32);
			this.btnStopPlan.Text = "Stop current plan, leave apps running";
			this.btnStopPlan.Click += new System.EventHandler(this.stopPlanMenuItem_Click);
			// 
			// btnKillPlan
			// 
			this.btnKillPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnKillPlan.Image = ((System.Drawing.Image)(resources.GetObject("btnKillPlan.Image")));
			this.btnKillPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnKillPlan.Name = "btnKillPlan";
			this.btnKillPlan.Size = new System.Drawing.Size(34, 32);
			this.btnKillPlan.Text = "Kill apps from current plan";
			this.btnKillPlan.Click += new System.EventHandler(this.killPlanMenuItem_Click);
			// 
			// btnRestartPlan
			// 
			this.btnRestartPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnRestartPlan.Image = ((System.Drawing.Image)(resources.GetObject("btnRestartPlan.Image")));
			this.btnRestartPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnRestartPlan.Name = "btnRestartPlan";
			this.btnRestartPlan.Size = new System.Drawing.Size(34, 32);
			this.btnRestartPlan.Text = "Restart current plan";
			this.btnRestartPlan.Click += new System.EventHandler(this.restartPlanMenuItem_Click);
			// 
			// bntAppsKillAll
			// 
			this.bntAppsKillAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.bntAppsKillAll.Image = ((System.Drawing.Image)(resources.GetObject("bntAppsKillAll.Image")));
			this.bntAppsKillAll.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.bntAppsKillAll.Name = "bntAppsKillAll";
			this.bntAppsKillAll.Size = new System.Drawing.Size(34, 32);
			this.bntAppsKillAll.Text = "Kill All";
			this.bntAppsKillAll.Click += new System.EventHandler(this.bntAppsKillAll_Click);
			// 
			// btnShowJustAppsFromCurrentPlan
			// 
			this.btnShowJustAppsFromCurrentPlan.CheckOnClick = true;
			this.btnShowJustAppsFromCurrentPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnShowJustAppsFromCurrentPlan.Image = ((System.Drawing.Image)(resources.GetObject("btnShowJustAppsFromCurrentPlan.Image")));
			this.btnShowJustAppsFromCurrentPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnShowJustAppsFromCurrentPlan.Name = "btnShowJustAppsFromCurrentPlan";
			this.btnShowJustAppsFromCurrentPlan.Size = new System.Drawing.Size(34, 32);
			this.btnShowJustAppsFromCurrentPlan.Text = "Show just apps from the current plan";
			// 
			// tabPagePlans
			// 
			this.tabPagePlans.Controls.Add(this.gridPlans);
			this.tabPagePlans.Controls.Add(this.toolStripPlans);
			this.tabPagePlans.Location = new System.Drawing.Point(4, 34);
			this.tabPagePlans.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPagePlans.Name = "tabPagePlans";
			this.tabPagePlans.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPagePlans.Size = new System.Drawing.Size(881, 415);
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
			this.gridPlans.Dock = System.Windows.Forms.DockStyle.Fill;
			this.gridPlans.FilterAndSortEnabled = true;
			this.gridPlans.FilterStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridPlans.Location = new System.Drawing.Point(3, 41);
			this.gridPlans.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.gridPlans.MultiSelect = false;
			this.gridPlans.Name = "gridPlans";
			this.gridPlans.ReadOnly = true;
			this.gridPlans.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.gridPlans.RowHeadersVisible = false;
			this.gridPlans.RowHeadersWidth = 72;
			this.gridPlans.RowTemplate.Height = 24;
			this.gridPlans.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.gridPlans.Size = new System.Drawing.Size(875, 370);
			this.gridPlans.SortStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridPlans.TabIndex = 6;
			this.gridPlans.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.gridPlans_CellFormatting);
			this.gridPlans.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gridPlans_MouseClick);
			this.gridPlans.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.gridPlans_MouseDoubleClick);
			// 
			// toolStripPlans
			// 
			this.toolStripPlans.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.toolStripPlans.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnPlansKillAll});
			this.toolStripPlans.Location = new System.Drawing.Point(3, 4);
			this.toolStripPlans.Name = "toolStripPlans";
			this.toolStripPlans.Size = new System.Drawing.Size(875, 37);
			this.toolStripPlans.TabIndex = 1;
			this.toolStripPlans.Text = "myToolStrip1";
			// 
			// btnPlansKillAll
			// 
			this.btnPlansKillAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnPlansKillAll.Image = ((System.Drawing.Image)(resources.GetObject("btnPlansKillAll.Image")));
			this.btnPlansKillAll.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnPlansKillAll.Name = "btnPlansKillAll";
			this.btnPlansKillAll.Size = new System.Drawing.Size(34, 32);
			this.btnPlansKillAll.Text = "Kill All";
			this.btnPlansKillAll.Click += new System.EventHandler(this.btnPlansKillAll_Click);
			// 
			// tabPageScripts
			// 
			this.tabPageScripts.Controls.Add(this.gridScripts);
			this.tabPageScripts.Controls.Add(this.toolStripScripts);
			this.tabPageScripts.Location = new System.Drawing.Point(4, 34);
			this.tabPageScripts.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPageScripts.Name = "tabPageScripts";
			this.tabPageScripts.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPageScripts.Size = new System.Drawing.Size(881, 415);
			this.tabPageScripts.TabIndex = 1;
			this.tabPageScripts.Text = "Scripts";
			this.tabPageScripts.UseVisualStyleBackColor = true;
			// 
			// gridScripts
			// 
			this.gridScripts.AllowUserToAddRows = false;
			this.gridScripts.AllowUserToDeleteRows = false;
			this.gridScripts.BackgroundColor = System.Drawing.SystemColors.Window;
			this.gridScripts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.gridScripts.Dock = System.Windows.Forms.DockStyle.Fill;
			this.gridScripts.FilterAndSortEnabled = true;
			this.gridScripts.FilterStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridScripts.Location = new System.Drawing.Point(3, 41);
			this.gridScripts.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.gridScripts.MultiSelect = false;
			this.gridScripts.Name = "gridScripts";
			this.gridScripts.ReadOnly = true;
			this.gridScripts.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.gridScripts.RowHeadersVisible = false;
			this.gridScripts.RowHeadersWidth = 72;
			this.gridScripts.RowTemplate.Height = 24;
			this.gridScripts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.gridScripts.Size = new System.Drawing.Size(875, 370);
			this.gridScripts.SortStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridScripts.TabIndex = 6;
			this.gridScripts.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.gridScripts_CellFormatting);
			this.gridScripts.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gridScripts_MouseClick);
			this.gridScripts.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.gridScripts_MouseDoubleClick);
			// 
			// toolStripScripts
			// 
			this.toolStripScripts.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.toolStripScripts.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnScriptsKillAll});
			this.toolStripScripts.Location = new System.Drawing.Point(3, 4);
			this.toolStripScripts.Name = "toolStripScripts";
			this.toolStripScripts.Size = new System.Drawing.Size(875, 37);
			this.toolStripScripts.TabIndex = 1;
			this.toolStripScripts.Text = "myToolStrip1";
			// 
			// btnScriptsKillAll
			// 
			this.btnScriptsKillAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnScriptsKillAll.Image = ((System.Drawing.Image)(resources.GetObject("btnScriptsKillAll.Image")));
			this.btnScriptsKillAll.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnScriptsKillAll.Name = "btnScriptsKillAll";
			this.btnScriptsKillAll.Size = new System.Drawing.Size(34, 32);
			this.btnScriptsKillAll.Text = "Kill All";
			this.btnScriptsKillAll.Click += new System.EventHandler(this.btnScriptsKillAll_Click);
			// 
			// tabPageMachs
			// 
			this.tabPageMachs.Controls.Add(this.gridMachs);
			this.tabPageMachs.Controls.Add(this.toolStripMachs);
			this.tabPageMachs.Location = new System.Drawing.Point(4, 34);
			this.tabPageMachs.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPageMachs.Name = "tabPageMachs";
			this.tabPageMachs.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPageMachs.Size = new System.Drawing.Size(881, 415);
			this.tabPageMachs.TabIndex = 1;
			this.tabPageMachs.Text = "Machines";
			this.tabPageMachs.UseVisualStyleBackColor = true;
			// 
			// gridMachs
			// 
			this.gridMachs.AllowUserToAddRows = false;
			this.gridMachs.AllowUserToDeleteRows = false;
			this.gridMachs.BackgroundColor = System.Drawing.SystemColors.Window;
			this.gridMachs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.gridMachs.Dock = System.Windows.Forms.DockStyle.Fill;
			this.gridMachs.FilterAndSortEnabled = true;
			this.gridMachs.FilterStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridMachs.Location = new System.Drawing.Point(3, 41);
			this.gridMachs.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.gridMachs.MultiSelect = false;
			this.gridMachs.Name = "gridMachs";
			this.gridMachs.ReadOnly = true;
			this.gridMachs.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.gridMachs.RowHeadersVisible = false;
			this.gridMachs.RowHeadersWidth = 72;
			this.gridMachs.RowTemplate.Height = 24;
			this.gridMachs.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.gridMachs.Size = new System.Drawing.Size(875, 370);
			this.gridMachs.SortStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridMachs.TabIndex = 6;
			this.gridMachs.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.gridMachs_CellFormatting);
			this.gridMachs.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gridMachs_MouseClick);
			this.gridMachs.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.gridMachs_MouseDoubleClick);
			// 
			// toolStripMachs
			// 
			this.toolStripMachs.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.toolStripMachs.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnMachsKillAll});
			this.toolStripMachs.Location = new System.Drawing.Point(3, 4);
			this.toolStripMachs.Name = "toolStripMachs";
			this.toolStripMachs.Size = new System.Drawing.Size(875, 37);
			this.toolStripMachs.TabIndex = 1;
			this.toolStripMachs.Text = "myToolStrip1";
			// 
			// btnMachsKillAll
			// 
			this.btnMachsKillAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnMachsKillAll.Image = ((System.Drawing.Image)(resources.GetObject("btnMachsKillAll.Image")));
			this.btnMachsKillAll.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnMachsKillAll.Name = "btnMachsKillAll";
			this.btnMachsKillAll.Size = new System.Drawing.Size(34, 32);
			this.btnMachsKillAll.Text = "Kill All";
			this.btnMachsKillAll.Click += new System.EventHandler(this.btnMachsKillAll_Click);
			// 
			// tabPageFiles
			// 
			this.tabPageFiles.Controls.Add(this.gridFiles);
			this.tabPageFiles.Controls.Add(this.toolStripFiles);
			this.tabPageFiles.Location = new System.Drawing.Point(4, 34);
			this.tabPageFiles.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPageFiles.Name = "tabPageFiles";
			this.tabPageFiles.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.tabPageFiles.Size = new System.Drawing.Size(881, 421);
			this.tabPageFiles.TabIndex = 1;
			this.tabPageFiles.Text = "Files";
			this.tabPageFiles.UseVisualStyleBackColor = true;
			// 
			// gridFiles
			// 
			this.gridFiles.AllowUserToAddRows = false;
			this.gridFiles.AllowUserToDeleteRows = false;
			this.gridFiles.BackgroundColor = System.Drawing.SystemColors.Window;
			this.gridFiles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.gridFiles.Dock = System.Windows.Forms.DockStyle.Fill;
			this.gridFiles.FilterAndSortEnabled = true;
			this.gridFiles.FilterStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridFiles.Location = new System.Drawing.Point(3, 41);
			this.gridFiles.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.gridFiles.MultiSelect = false;
			this.gridFiles.Name = "gridFiles";
			this.gridFiles.ReadOnly = true;
			this.gridFiles.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.gridFiles.RowHeadersVisible = false;
			this.gridFiles.RowHeadersWidth = 72;
			this.gridFiles.RowTemplate.Height = 24;
			this.gridFiles.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.gridFiles.Size = new System.Drawing.Size(875, 376);
			this.gridFiles.SortStringChangedInvokeBeforeDatasourceUpdate = true;
			this.gridFiles.TabIndex = 6;
			this.gridFiles.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.gridFiles_CellFormatting);
			this.gridFiles.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gridFiles_MouseClick);
			this.gridFiles.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.gridFiles_MouseDoubleClick);
			// 
			// toolStripFiles
			// 
			this.toolStripFiles.ImageScalingSize = new System.Drawing.Size(28, 28);
			this.toolStripFiles.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnFilesKillAll});
			this.toolStripFiles.Location = new System.Drawing.Point(3, 4);
			this.toolStripFiles.Name = "toolStripFiles";
			this.toolStripFiles.Size = new System.Drawing.Size(875, 37);
			this.toolStripFiles.TabIndex = 1;
			this.toolStripFiles.Text = "myToolStrip1";
			// 
			// btnFilesKillAll
			// 
			this.btnFilesKillAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			this.btnFilesKillAll.Image = ((System.Drawing.Image)(resources.GetObject("btnFilesKillAll.Image")));
			this.btnFilesKillAll.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.btnFilesKillAll.Name = "btnFilesKillAll";
			this.btnFilesKillAll.Size = new System.Drawing.Size(34, 32);
			this.btnFilesKillAll.Text = "Kill All";
			this.btnFilesKillAll.Click += new System.EventHandler(this.btnFilesKillAll_Click);
			// 
			// stblbMain
			// 
			this.stblbMain.AutoSize = false;
			this.stblbMain.Name = "stblbMain";
			this.stblbMain.Size = new System.Drawing.Size(300, 25);
			this.stblbMain.Text = "stblbMain";
			this.stblbMain.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// stblbSSH
			// 
			this.stblbSSH.Name = "stblbSSH";
			this.stblbSSH.Size = new System.Drawing.Size(524, 25);
			this.stblbSSH.Spring = true;
			this.stblbSSH.Text = "stblbSSH";
			this.stblbSSH.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// frmMain
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(889, 516);
			this.Controls.Add(this.tabControl1);
			this.Controls.Add(this.statusStrip);
			this.Controls.Add(this.menuMain);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MainMenuStrip = this.menuMain;
			this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.Name = "frmMain";
			this.Text = "Dirigent";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.frmMain_FormClosed);
			this.Load += new System.EventHandler(this.frmMain_Load);
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
			this.tabPageScripts.ResumeLayout(false);
			this.tabPageScripts.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridScripts)).EndInit();
			this.toolStripScripts.ResumeLayout(false);
			this.toolStripScripts.PerformLayout();
			this.tabPageMachs.ResumeLayout(false);
			this.tabPageMachs.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridMachs)).EndInit();
			this.toolStripMachs.ResumeLayout(false);
			this.toolStripMachs.PerformLayout();
			this.tabPageFiles.ResumeLayout(false);
			this.tabPageFiles.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.gridFiles)).EndInit();
			this.toolStripFiles.ResumeLayout(false);
			this.toolStripFiles.PerformLayout();
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
		private System.Windows.Forms.Timer tmrTick;
		private System.Windows.Forms.ToolStripMenuItem stopPlanToolStripMenuItem;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage tabPageApps;
		private Dirigent.Gui.WinForms.MyToolStrip toolStripApps;
		private System.Windows.Forms.ToolStripButton btnSelectPlan;
		private System.Windows.Forms.ToolStripButton btnStartPlan;
		private System.Windows.Forms.ToolStripButton btnStopPlan;
		private System.Windows.Forms.ToolStripButton btnKillPlan;
		private System.Windows.Forms.ToolStripButton btnRestartPlan;
		private System.Windows.Forms.ToolStripButton bntAppsKillAll;
		private System.Windows.Forms.ToolStripButton btnShowJustAppsFromCurrentPlan;
		private Dirigent.Gui.WinForms.MyToolStrip toolStripPlans;
		private System.Windows.Forms.ToolStripButton btnPlansKillAll;
		private Dirigent.Gui.WinForms.MyToolStrip toolStripScripts;
		private System.Windows.Forms.ToolStripButton btnScriptsKillAll;
		private Dirigent.Gui.WinForms.MyToolStrip toolStripMachs;
		private System.Windows.Forms.ToolStripButton btnMachsKillAll;
		private Dirigent.Gui.WinForms.MyToolStrip toolStripFiles;
		private System.Windows.Forms.ToolStripButton btnFilesKillAll;
		private System.Windows.Forms.TabPage tabPagePlans;
		private System.Windows.Forms.TabPage tabPageScripts;
		private System.Windows.Forms.TabPage tabPageMachs;
		private System.Windows.Forms.TabPage tabPageFiles;
		private Zuby.ADGV.AdvancedDataGridView gridApps;
		private Zuby.ADGV.AdvancedDataGridView gridPlans;
		private Zuby.ADGV.AdvancedDataGridView gridScripts;
		private Zuby.ADGV.AdvancedDataGridView gridMachs;
		private Zuby.ADGV.AdvancedDataGridView gridFiles;
		private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem1;
		private System.Windows.Forms.ToolStripStatusLabel stblbMain;
		private System.Windows.Forms.ToolStripStatusLabel stblbSSH;
	}
}

