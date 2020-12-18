namespace Dirigent.Reinstaller
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
			this.btnRelaunch = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.chkAllAtOnce = new System.Windows.Forms.CheckBox();
			this.SuspendLayout();
			// 
			// btnRelaunch
			// 
			this.btnRelaunch.Location = new System.Drawing.Point(58, 112);
			this.btnRelaunch.Name = "btnRelaunch";
			this.btnRelaunch.Size = new System.Drawing.Size(218, 80);
			this.btnRelaunch.TabIndex = 0;
			this.btnRelaunch.Text = "Relaunch Dirigent";
			this.btnRelaunch.UseVisualStyleBackColor = true;
			this.btnRelaunch.Click += new System.EventHandler(this.btnRelaunch_Click);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(53, 28);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(358, 25);
			this.label1.TabIndex = 1;
			this.label1.Text = "Now it\'s time to replace the Dirigent files!";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(53, 68);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(757, 25);
			this.label2.TabIndex = 2;
			this.label2.Text = "Click the button below once the Dirignet files have been overwritten with a new v" +
    "ersion.";
			// 
			// chkAllAtOnce
			// 
			this.chkAllAtOnce.AutoSize = true;
			this.chkAllAtOnce.Checked = true;
			this.chkAllAtOnce.CheckState = System.Windows.Forms.CheckState.Checked;
			this.chkAllAtOnce.Location = new System.Drawing.Point(321, 143);
			this.chkAllAtOnce.Name = "chkAllAtOnce";
			this.chkAllAtOnce.Size = new System.Drawing.Size(210, 29);
			this.chkAllAtOnce.TabIndex = 3;
			this.chkAllAtOnce.Text = "Do for all computers";
			this.chkAllAtOnce.UseVisualStyleBackColor = true;
			// 
			// frmMain
			// 
			this.AcceptButton = this.btnRelaunch;
			this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1074, 242);
			this.Controls.Add(this.chkAllAtOnce);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.btnRelaunch);
			this.Name = "frmMain";
			this.Text = "Dirigent Reinstaller";
			this.Load += new System.EventHandler(this.frmMain_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Button btnRelaunch;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.CheckBox chkAllAtOnce;
	}
}

