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
            this.startToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stopPlanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restartToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.killPlanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.selectPlanToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lstvApps = new System.Windows.Forms.ListView();
            this.hdrName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.hdrStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ctxmAppList = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.runToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.killToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restartToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.tmrTick = new System.Windows.Forms.Timer(this.components);
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnSelectPlan = new System.Windows.Forms.ToolStripButton();
            this.btnStartPlan = new System.Windows.Forms.ToolStripButton();
            this.btnStopPlan = new System.Windows.Forms.ToolStripButton();
            this.btnKillPlan = new System.Windows.Forms.ToolStripButton();
            this.btnRestartPlan = new System.Windows.Forms.ToolStripButton();
            this.statusStrip.SuspendLayout();
            this.menuMain.SuspendLayout();
            this.ctxmAppList.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1});
            this.statusStrip.Location = new System.Drawing.Point(0, 233);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(345, 22);
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
            this.menuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.planToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuMain.Location = new System.Drawing.Point(0, 0);
            this.menuMain.Name = "menuMain";
            this.menuMain.Size = new System.Drawing.Size(345, 28);
            this.menuMain.TabIndex = 1;
            this.menuMain.Text = "menuStrip1";
            // 
            // planToolStripMenuItem
            // 
            this.planToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startToolStripMenuItem,
            this.stopPlanToolStripMenuItem,
            this.restartToolStripMenuItem,
            this.killPlanToolStripMenuItem,
            this.toolStripMenuItem1,
            this.selectPlanToolStripMenuItem});
            this.planToolStripMenuItem.Name = "planToolStripMenuItem";
            this.planToolStripMenuItem.Size = new System.Drawing.Size(49, 24);
            this.planToolStripMenuItem.Text = "Plan";
            // 
            // startToolStripMenuItem
            // 
            this.startToolStripMenuItem.Name = "startToolStripMenuItem";
            this.startToolStripMenuItem.Size = new System.Drawing.Size(124, 24);
            this.startToolStripMenuItem.Text = "Start";
            this.startToolStripMenuItem.Click += new System.EventHandler(this.startToolStripMenuItem_Click);
            // 
            // stopPlanToolStripMenuItem
            // 
            this.stopPlanToolStripMenuItem.Name = "stopPlanToolStripMenuItem";
            this.stopPlanToolStripMenuItem.Size = new System.Drawing.Size(124, 24);
            this.stopPlanToolStripMenuItem.Text = "Stop";
            this.stopPlanToolStripMenuItem.Click += new System.EventHandler(this.stopPlanToolStripMenuItem_Click);
            // 
            // restartToolStripMenuItem
            // 
            this.restartToolStripMenuItem.Name = "restartToolStripMenuItem";
            this.restartToolStripMenuItem.Size = new System.Drawing.Size(124, 24);
            this.restartToolStripMenuItem.Text = "Restart";
            this.restartToolStripMenuItem.Click += new System.EventHandler(this.restartToolStripMenuItem_Click);
            // 
            // killPlanToolStripMenuItem
            // 
            this.killPlanToolStripMenuItem.Name = "killPlanToolStripMenuItem";
            this.killPlanToolStripMenuItem.Size = new System.Drawing.Size(124, 24);
            this.killPlanToolStripMenuItem.Text = "Kill";
            this.killPlanToolStripMenuItem.Click += new System.EventHandler(this.killPlanToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(121, 6);
            // 
            // selectPlanToolStripMenuItem
            // 
            this.selectPlanToolStripMenuItem.Name = "selectPlanToolStripMenuItem";
            this.selectPlanToolStripMenuItem.Size = new System.Drawing.Size(124, 24);
            this.selectPlanToolStripMenuItem.Text = "Select";
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(53, 24);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(119, 24);
            this.aboutToolStripMenuItem.Text = "&About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // lstvApps
            // 
            this.lstvApps.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lstvApps.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.hdrName,
            this.hdrStatus});
            this.lstvApps.FullRowSelect = true;
            this.lstvApps.GridLines = true;
            this.lstvApps.Location = new System.Drawing.Point(0, 31);
            this.lstvApps.Name = "lstvApps";
            this.lstvApps.Size = new System.Drawing.Size(345, 202);
            this.lstvApps.TabIndex = 2;
            this.lstvApps.UseCompatibleStateImageBehavior = false;
            this.lstvApps.View = System.Windows.Forms.View.Details;
            this.lstvApps.MouseClick += new System.Windows.Forms.MouseEventHandler(this.lstvApps_MouseClick);
            this.lstvApps.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.lstvApps_MouseDoubleClick);
            // 
            // hdrName
            // 
            this.hdrName.Text = "Application Name";
            this.hdrName.Width = 241;
            // 
            // hdrStatus
            // 
            this.hdrStatus.Text = "Status";
            this.hdrStatus.Width = 100;
            // 
            // ctxmAppList
            // 
            this.ctxmAppList.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.runToolStripMenuItem,
            this.killToolStripMenuItem,
            this.restartToolStripMenuItem1});
            this.ctxmAppList.Name = "ctxmAppList";
            this.ctxmAppList.Size = new System.Drawing.Size(125, 76);
            // 
            // runToolStripMenuItem
            // 
            this.runToolStripMenuItem.Name = "runToolStripMenuItem";
            this.runToolStripMenuItem.Size = new System.Drawing.Size(124, 24);
            this.runToolStripMenuItem.Text = "&Start";
            // 
            // killToolStripMenuItem
            // 
            this.killToolStripMenuItem.Name = "killToolStripMenuItem";
            this.killToolStripMenuItem.Size = new System.Drawing.Size(124, 24);
            this.killToolStripMenuItem.Text = "&Stop";
            // 
            // restartToolStripMenuItem1
            // 
            this.restartToolStripMenuItem1.Name = "restartToolStripMenuItem1";
            this.restartToolStripMenuItem1.Size = new System.Drawing.Size(124, 24);
            this.restartToolStripMenuItem1.Text = "&Restart";
            // 
            // tmrTick
            // 
            this.tmrTick.Interval = 500;
            this.tmrTick.Tick += new System.EventHandler(this.tmrTick_Tick);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnSelectPlan,
            this.btnStartPlan,
            this.btnStopPlan,
            this.btnKillPlan,
            this.btnRestartPlan});
            this.toolStrip1.Location = new System.Drawing.Point(0, 28);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(345, 25);
            this.toolStrip1.TabIndex = 3;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnSelectPlan
            // 
            this.btnSelectPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnSelectPlan.Image = global::Dirigent.Agent.Gui.Resource1.open;
            this.btnSelectPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSelectPlan.Name = "btnSelectPlan";
            this.btnSelectPlan.Size = new System.Drawing.Size(23, 22);
            this.btnSelectPlan.Text = "Select Plan";
            this.btnSelectPlan.Click += new System.EventHandler(this.btnSelectPlan_Click);
            // 
            // btnStartPlan
            // 
            this.btnStartPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnStartPlan.Image = global::Dirigent.Agent.Gui.Resource1.play;
            this.btnStartPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnStartPlan.Name = "btnStartPlan";
            this.btnStartPlan.Size = new System.Drawing.Size(23, 22);
            this.btnStartPlan.Text = "Start Plan";
            this.btnStartPlan.Click += new System.EventHandler(this.startToolStripMenuItem_Click);
            // 
            // btnStopPlan
            // 
            this.btnStopPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnStopPlan.Image = global::Dirigent.Agent.Gui.Resource1.stop;
            this.btnStopPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnStopPlan.Name = "btnStopPlan";
            this.btnStopPlan.Size = new System.Drawing.Size(23, 22);
            this.btnStopPlan.Text = "Stop Plan";
            this.btnStopPlan.Click += new System.EventHandler(this.stopPlanToolStripMenuItem_Click);
            // 
            // btnKillPlan
            // 
            this.btnKillPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnKillPlan.Image = global::Dirigent.Agent.Gui.Resource1.delete;
            this.btnKillPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnKillPlan.Name = "btnKillPlan";
            this.btnKillPlan.Size = new System.Drawing.Size(23, 22);
            this.btnKillPlan.Text = "Kill Plan";
            this.btnKillPlan.Click += new System.EventHandler(this.killPlanToolStripMenuItem_Click);
            // 
            // btnRestartPlan
            // 
            this.btnRestartPlan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.btnRestartPlan.Image = global::Dirigent.Agent.Gui.Resource1.refresh;
            this.btnRestartPlan.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRestartPlan.Name = "btnRestartPlan";
            this.btnRestartPlan.Size = new System.Drawing.Size(23, 22);
            this.btnRestartPlan.Text = "Restart Plan";
            this.btnRestartPlan.Click += new System.EventHandler(this.restartToolStripMenuItem_Click);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(345, 255);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.lstvApps);
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
            this.ctxmAppList.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.MenuStrip menuMain;
        private System.Windows.Forms.ToolStripMenuItem planToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem startToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem killPlanToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem restartToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem selectPlanToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ListView lstvApps;
        private System.Windows.Forms.ColumnHeader hdrName;
        private System.Windows.Forms.ColumnHeader hdrStatus;
        private System.Windows.Forms.ContextMenuStrip ctxmAppList;
        private System.Windows.Forms.ToolStripMenuItem runToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem killToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem restartToolStripMenuItem1;
        private System.Windows.Forms.Timer tmrTick;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripMenuItem stopPlanToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnSelectPlan;
        private System.Windows.Forms.ToolStripButton btnStartPlan;
        private System.Windows.Forms.ToolStripButton btnStopPlan;
        private System.Windows.Forms.ToolStripButton btnKillPlan;
        private System.Windows.Forms.ToolStripButton btnRestartPlan;
    }
}

