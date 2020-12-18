using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace Dirigent.Reinstaller
{
	public partial class frmMain : Form
	{
		string paramFileName; // tmp file to be deleted once read
		string exePath;
		string cmdArgs;
		string cwdPath;
		string downloadUrl;
		string downloadMode;
		string masterIP;
		int masterPort = -1;
		UdpClient udpClient;


		public frmMain()
		{
			InitializeComponent();

		}

		private void frmMain_Load(object sender, EventArgs e)
		{
			var args = Environment.GetCommandLineArgs();
			if( args.Length <= 1 )
			{
				MessageBox.Show("Missing argument on cmd line!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error );
				this.BeginInvoke(new MethodInvoker(this.Close));
			}

			paramFileName = args[1];
			loadParamsFromFile( paramFileName );
			initNet();

		}

		void initNet()
		{
			udpClient = new UdpClient();
			udpClient.ExclusiveAddressUse = false;
			udpClient.Client.Bind(new IPEndPoint( IPAddress.Any, masterPort));


			try
			{
				 udpClient.BeginReceive(new AsyncCallback(recv), null);
			}
			catch(Exception e)
			{
				 MessageBox.Show(e.ToString());
			}
		}

		const string MAGIC = "Dirigent.Reinstaller:";
		const string CMD_RELAUNCH = "Relaunch";

		void sendRelaunch()
		{
			Byte[] sendBytes = Encoding.ASCII.GetBytes( MAGIC + CMD_RELAUNCH );
			udpClient.Send( sendBytes, sendBytes.Length, "255.255.255.255", masterPort );
		}

		private void recv(IAsyncResult res)
		{
			IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, masterPort);
			byte[] receivedBytes = udpClient.EndReceive(res, ref RemoteIpEndPoint);
			udpClient.BeginReceive(new AsyncCallback(recv), null);
			
			string recvString = Encoding.ASCII.GetString(receivedBytes);
			
			// ignore foreign packets
			if( !recvString.StartsWith(MAGIC) )
				return;

			string cmd = recvString.Substring( MAGIC.Length );

			if( cmd == CMD_RELAUNCH )
			{
				if( relaunch() )
				{
					// success, terminate itself
					this.BeginInvoke(new MethodInvoker(this.Close));
				}
			}
		}

		void loadParamsFromFile( string fname )
		{
			try
			{
				var fileLines = System.IO.File.ReadAllLines( fname );
				List<string> lines = new List<string>( fileLines );
				for(int i=fileLines.Length; i <= 10; i++) lines.Add( string.Empty ); // replace missing lines with empty string

				exePath = lines[0].Trim();
				cmdArgs = lines[1].Trim();
				cwdPath = lines[2].Trim();
				downloadMode = lines[3].Trim();
				downloadUrl = lines[4].Trim();
				masterIP = lines[5].Trim();
				Int32.TryParse( lines[6].Trim(), out masterPort );

				System.IO.File.Delete( fname );
			}
			catch( Exception ex )
			{
				MessageBox.Show(String.Format("Error loading parameters from file {0}", fname), this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error );
				this.BeginInvoke(new MethodInvoker(this.Close));
			}


		}

		// returns true if we really started the process
		bool relaunch()
		{
            var psi = new ProcessStartInfo();
			psi.FileName =  exePath;
            psi.Arguments = cmdArgs;
            psi.WorkingDirectory = cwdPath;
            psi.WindowStyle = ProcessWindowStyle.Normal;
			psi.UseShellExecute = false; // allows us using environment variables
			Process proc = null;
			try
            {
                //log.DebugFormat("StartProc exe \"{0}\", cmd \"{1}\", dir \"{2}\", windowstyle {3}", psi.FileName, psi.Arguments, psi.WorkingDirectory, psi.WindowStyle );
                proc = Process.Start(psi);
                if( proc != null )
                {
                    //log.DebugFormat("StartProc SUCCESS pid {0}", proc.Id );
					return true;
                }
                else
                {
                    //log.DebugFormat("StartProc FAILED (no details)" );
                    proc = null;
					return false;
                }
            }
            catch (Exception ex)
            {
                //log.DebugFormat("StartProc FAILED except {0}", ex.Message );
                MessageBox.Show( String.Format("Failed to run Dirigent process\nexe: {0}\nargs: {1}\ncwd:{2}", psi.FileName, psi.Arguments, psi.WorkingDirectory));
            }
			return false;
		}
		
		private void btnRelaunch_Click(object sender, EventArgs e)
		{
			if( chkAllAtOnce.Checked )
			{
				sendRelaunch();	// we will receive this as well and process it in network handler
				return;
			}
			else
			{
				if( relaunch() )
				{
					// success, terminate itself
					this.BeginInvoke(new MethodInvoker(this.Close));
				}
			}
		}

	}
}
