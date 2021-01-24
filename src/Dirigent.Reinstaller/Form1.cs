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

namespace Dirigent.Reinstaller {
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
	int parentPID = -1;
	UdpClient udpClient;
	enum EMode { Init, WaitingForParentToDie, MakeReadyForRelaunch, WaitingForRelaunchRequests, Relaunch, Close }
	EMode mode = EMode.Init;


	public frmMain()
	{
		InitializeComponent();
		//System.Diagnostics.Debugger.Launch();

	}

	private void frmMain_Load( object sender, EventArgs e )
	{
		var args = Environment.GetCommandLineArgs();
		if ( args.Length <= 1 )
		{
			MessageBox.Show( "Missing argument on cmd line!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error );
			this.BeginInvoke( new MethodInvoker( this.Close ) );
		}

		paramFileName = args[1];
		loadParamsFromFile( paramFileName );

	}

	void initNet()
	{
		udpClient = new UdpClient();
		udpClient.ExclusiveAddressUse = false;
		udpClient.EnableBroadcast = true;
		udpClient.Client.Bind( new IPEndPoint( IPAddress.Any, masterPort ) );


		try
		{
			udpClient.BeginReceive( new AsyncCallback( recv ), null );
		}
		catch ( Exception e )
		{
			MessageBox.Show( e.ToString() );
		}
	}

	void doneNet()
	{
		udpClient.Close();
		udpClient = null;
	}


	const string MAGIC = "Dirigent.Reinstaller:";
	const string CMD_RELAUNCH = "Relaunch";

	void sendRelaunch()
	{
		Byte[] sendBytes = Encoding.ASCII.GetBytes( MAGIC + CMD_RELAUNCH );
		udpClient.Send( sendBytes, sendBytes.Length, "255.255.255.255", masterPort );
	}

	private void recv( IAsyncResult res )
	{
		if ( udpClient == null ) return;

		try	// necessary to catch the exception when the socket is closed
		{
			IPEndPoint RemoteIpEndPoint = new IPEndPoint( IPAddress.Any, masterPort );
			byte[] receivedBytes = udpClient.EndReceive( res, ref RemoteIpEndPoint );
			udpClient.BeginReceive( new AsyncCallback( recv ), null );

			string recvString = Encoding.ASCII.GetString( receivedBytes );

			// ignore foreign packets
			if ( !recvString.StartsWith( MAGIC ) )
				return;

			string cmd = recvString.Substring( MAGIC.Length );

			if ( cmd == CMD_RELAUNCH )
			{
				mode = EMode.Relaunch;
			}
		}
		catch
		{
		}
	}

	void loadParamsFromFile( string fname )
	{
		try
		{
			var fileLines = System.IO.File.ReadAllLines( fname );
			List<string> lines = new List<string>( fileLines );
			for ( int i = fileLines.Length; i <= 10; i++ ) lines.Add( string.Empty ); // replace missing lines with empty string

			exePath = lines[0].Trim();
			cmdArgs = lines[1].Trim();
			cwdPath = lines[2].Trim();
			downloadMode = lines[3].Trim();
			downloadUrl = lines[4].Trim();
			masterIP = lines[5].Trim();
			Int32.TryParse( lines[6].Trim(), out masterPort );
			Int32.TryParse( lines[7].Trim(), out parentPID );

			System.IO.File.Delete( fname );
		}
		catch ( Exception ex )
		{
			MessageBox.Show( String.Format( "Error loading parameters from file {0} ({1})", fname, ex.Message ), this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error );
			this.BeginInvoke( new MethodInvoker( this.Close ) );
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
			proc = Process.Start( psi );
			if ( proc != null )
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
		catch ( Exception ex )
		{
			//log.DebugFormat("StartProc FAILED except {0}", ex.Message );
			MessageBox.Show( String.Format( "Failed to run Dirigent process\nexe: {0}\nargs: {1}\ncwd:{2} ({3})", psi.FileName, psi.Arguments, psi.WorkingDirectory, ex.Message ) );
		}
		return false;
	}

	private void btnRelaunch_Click( object sender, EventArgs e )
	{
		if ( chkAllAtOnce.Checked )
		{
			sendRelaunch();	// we will receive this as well and process it in network handler
			return;
		}
		else
		{
			mode = EMode.Relaunch;
		}
	}

	bool hasParentExited()
	{
		Process p = null;
		try
		{
			p = Process.GetProcessById( parentPID );
		}
		catch
		{
		}

		if ( p == null || p.HasExited )
		{
			return true;
		}
		return false;
	}

	private void timer1_Tick( object sender, EventArgs e )
	{
		switch ( mode )
		{
			case EMode.Init:
			{
				label1.Text = "Waiting for Dirigent to terminate...";
				label2.Text = "";
				btnRelaunch.Enabled = false;
				chkAllAtOnce.Enabled = false;

				mode = EMode.WaitingForParentToDie;
				break;
			}

			case EMode.WaitingForParentToDie:
			{
				if ( hasParentExited() )
				{
					mode = EMode.MakeReadyForRelaunch;
				}

				break;
			}

			case EMode.MakeReadyForRelaunch:
			{
				label1.Text = "Now it's time to replace the Dirigent files!";
				label2.Text = "Click the button below once the Dirignet files have been overwritten with a new version.";
				btnRelaunch.Enabled = true;
				chkAllAtOnce.Enabled = true;

				initNet();

				mode = EMode.WaitingForRelaunchRequests;
				break;
			};

			case EMode.WaitingForRelaunchRequests:
			{
				// either button press or network packet moves us to the next state
				break;
			}

			case EMode.Relaunch:
			{
				// close socket so it does not get inherited by the new dirigent
				// (would cause exception when relaucher is started again from this newly lauched dirigent)
				doneNet();

				if ( relaunch() )
				{
					// success, terminate itself
					mode = EMode.Close;
				}
				else
				{
					mode = EMode.MakeReadyForRelaunch;
				}
				break;
			}

			case EMode.Close:
			{
				this.Close();
				break;
			}
		}
	}
}
}
