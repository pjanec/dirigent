using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dirigent.Gui.WinForms
{
	public partial class SshDisconnectDlg : Form
	{
		GatewayManager _gatewayManager;

		public SshDisconnectDlg( GatewayManager gatewayManager )
		{
			_gatewayManager = gatewayManager;
			InitializeComponent();
		}

		private void SshConnectDlg_Load( object sender, EventArgs e )
		{
			var sess = _gatewayManager.CurrentSession;
			if (sess != null)
			{
				var gw = sess.Gateway;
				txtGateway.Text = $"{gw.Label} [{gw.ExternalIP}]";
			}
			else
			{
				txtGateway.Text = "";
			}
		}

		private void btnDisconnect_Click( object sender, EventArgs e )
		{
			_gatewayManager.Disconnect();

			DialogResult = DialogResult.OK;
			Close();
		}

		private void btnCancel_Click( object sender, EventArgs e )
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
