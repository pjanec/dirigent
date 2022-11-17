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
			this.rtbAppProps = new System.Windows.Forms.RichTextBox();
			this.SuspendLayout();
			// 
			// rtbAppProps
			// 
			this.rtbAppProps.Dock = System.Windows.Forms.DockStyle.Fill;
			this.rtbAppProps.Location = new System.Drawing.Point(0, 0);
			this.rtbAppProps.Name = "rtbAppProps";
			this.rtbAppProps.ReadOnly = true;
			this.rtbAppProps.Size = new System.Drawing.Size(800, 450);
			this.rtbAppProps.TabIndex = 0;
			this.rtbAppProps.Text = "";
			// 
			// frmAppProperties
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(800, 450);
			this.Controls.Add(this.rtbAppProps);
			this.Name = "frmAppProperties";
			this.Text = "Properties";
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.RichTextBox rtbAppProps;
	}
}