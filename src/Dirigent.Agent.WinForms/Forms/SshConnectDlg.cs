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
	public partial class SshConnectDlg : Form
	{
		GatewayManager _gatewayManager;
		List<GatewayDef> _gateways;
		public GatewayDef SelectedGateway;

		
		
		public SshConnectDlg( GatewayManager gatewayManager )
		{
			_gatewayManager = gatewayManager;
			InitializeComponent();
		}

		private void SshConnectDlg_Load( object sender, EventArgs e )
		{
			_gateways = _gatewayManager.Gateways.ToList();
			
			listGateways.Items.Clear();
			foreach( var gw in _gateways )
			{
				listGateways.Items.Add( $"{gw.Label} [{gw.ExternalIP}]" );
			}
			listGateways.SelectedIndex = listGateways.Items.Count > 0 ? 0 : -1;
		}

		void ConnectToSelected()
		{
			var idx = listGateways.SelectedIndex;
			if( idx < 0 || idx >= _gateways.Count ) return;
			var gw = _gateways[idx];

			SelectedGateway = gw;
			DialogResult = DialogResult.OK;
			Close();
		}

		private void btnConnect_Click( object sender, EventArgs e )
		{
			ConnectToSelected();
		}

		private void btnCancel_Click( object sender, EventArgs e )
		{
			SelectedGateway = null;
			DialogResult = DialogResult.Cancel;
			Close();
		}

		private void listGateways_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			ConnectToSelected();
		}
	}
}
