using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Dirigent.Common;

namespace Dirigent.Agent.Core {
/// <summary>
/// Agent handles local applications lauching and killing according to the launch plan
/// and monitoring the status of local applications.
/// </summary>
public class ReinstallLauncher
{
	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

	public void Launch(
	    string mode,
	    string url,
	    string masterIP,
	    int masterPort
	)
	{
		var tmpFile = System.IO.Path.GetTempFileName();
		var exePath = Tools.GetExePath();
		var exeDir = System.IO.Path.GetDirectoryName( exePath );
		var rawCmd = Environment.CommandLine;
		var cmdl0 = Environment.GetCommandLineArgs()[0];
		string argsOnly = "";
		if ( rawCmd.StartsWith( cmdl0 ) )
		{
			argsOnly = rawCmd.Substring( cmdl0.Length ).TrimStart();
		}
		else if ( rawCmd.StartsWith( "\"" + cmdl0 + "\"" ) )
		{
			argsOnly = rawCmd.Substring( cmdl0.Length + 2 ).TrimStart();
		}
		else
		{
			var args = Environment.GetCommandLineArgs();
			var justArgs = new List<string>();
			for ( int i = 1; i < args.Length; i++ )
			{
				justArgs.Add( args[i] );
			}
			argsOnly = string.Join( " ", justArgs );
		}
		var cwd = System.IO.Directory.GetCurrentDirectory();

		using ( System.IO.StreamWriter file = new System.IO.StreamWriter( tmpFile ) )
		{
			file.WriteLine(	exePath );
			file.WriteLine(	argsOnly );
			file.WriteLine( cwd );
			file.WriteLine( mode );
			file.WriteLine( url );
			file.WriteLine( masterIP );
			file.WriteLine( masterPort );
			file.WriteLine( Process.GetCurrentProcess().Id );
		}

		// run restarter process (it is responsible for deleting the temp file passed as argument)
		string exeName = "Dirigent.Reinstaller.exe";
		var appPath = exeDir + "\\" + exeName;

		// copy to temp dir to allow overwriting with a new version
		var tmpExePath = System.IO.Path.GetTempPath() + exeName;
		System.IO.File.Copy( appPath, tmpExePath, true );

		var psi = new ProcessStartInfo();
		psi.FileName = tmpExePath;
		psi.Arguments = "\"" + tmpFile + "\"";
		psi.WorkingDirectory = System.IO.Path.GetTempPath();
		psi.WindowStyle = ProcessWindowStyle.Normal;
		psi.UseShellExecute = false; // allows us using environment variables
		Process proc = null;
		try
		{
			log.DebugFormat( "StartProc exe \"{0}\", cmd \"{1}\", dir \"{2}\", windowstyle {3}", psi.FileName, psi.Arguments, psi.WorkingDirectory, psi.WindowStyle );
			proc = Process.Start( psi );
			if ( proc != null )
			{
				log.DebugFormat( "StartProc SUCCESS pid {0}", proc.Id );
			}
			else
			{
				log.DebugFormat( "StartProc FAILED (no details)" );
				proc = null;
				return;
			}
		}
		catch ( Exception ex )
		{
			log.DebugFormat( "StartProc FAILED except {0}", ex.Message );
			throw new Exception( String.Format( "Failed to run Dirigent Reinstaller process {0} from {1}", psi.FileName, psi.WorkingDirectory ) );
		}

		//STARTUPINFO si = new STARTUPINFO();
		//si.cb = Marshal.SizeOf(si);
		//si.dwFlags = STARTF_USESTDHANDLES;

		//PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

		//CreateProcess(
		//	tmpExePath,
		//	exeName + " " + tmpFile,
		//	IntPtr.Zero,
		//	IntPtr.Zero,
		//	false,
		//	NORMAL_PRIORITY_CLASS,
		//	IntPtr.Zero,
		//	cwd,
		//	ref si,
		//	out pi
		//);

	}

	//// This also works with CharSet.Ansi as long as the calling function uses the same character set.
	//[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	//public struct STARTUPINFOEX
	//{
	//	 public STARTUPINFO StartupInfo;
	//	 public IntPtr lpAttributeList;
	//}

	//// This also works with CharSet.Ansi as long as the calling function uses the same character set.
	//[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	//public struct STARTUPINFO
	//{
	//	 public Int32 cb;
	//	 public string lpReserved;
	//	 public string lpDesktop;
	//	 public string lpTitle;
	//	 public Int32 dwX;
	//	 public Int32 dwY;
	//	 public Int32 dwXSize;
	//	 public Int32 dwYSize;
	//	 public Int32 dwXCountChars;
	//	 public Int32 dwYCountChars;
	//	 public Int32 dwFillAttribute;
	//	 public uint dwFlags;
	//	 public Int16 wShowWindow;
	//	 public Int16 cbReserved2;
	//	 public IntPtr lpReserved2;
	//	 public IntPtr hStdInput;
	//	 public IntPtr hStdOutput;
	//	 public IntPtr hStdError;
	//}

	//[StructLayout(LayoutKind.Sequential)]
	//internal struct PROCESS_INFORMATION
	//{
	//   public IntPtr hProcess;
	//   public IntPtr hThread;
	//   public int dwProcessId;
	//   public int dwThreadId;
	//}

	//[StructLayout(LayoutKind.Sequential)]
	//public struct SECURITY_ATTRIBUTES
	//{
	//	public int nLength;
	//	public IntPtr lpSecurityDescriptor;
	//	public int bInheritHandle;
	//}


	//[DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Auto)]
	//static extern bool CreateProcess(
	//	string lpApplicationName,
	//	string lpCommandLine,
	//	IntPtr lpProcessAttributes, //ref SECURITY_ATTRIBUTES lpProcessAttributes,
	//	IntPtr lpThreadAttributes, // ref SECURITY_ATTRIBUTES lpThreadAttributes,
	//	bool bInheritHandles,
	//	uint dwCreationFlags,
	//	IntPtr lpEnvironment,
	//	string lpCurrentDirectory,
	//	[In] ref STARTUPINFO lpStartupInfo,
	//	out PROCESS_INFORMATION lpProcessInformation);


	//const uint NORMAL_PRIORITY_CLASS			= 0x00000020;
	//const uint BELOW_NORMAL_PRIORITY_CLASS      = 0x00004000;
	//const uint ABOVE_NORMAL_PRIORITY_CLASS      = 0x00008000;


	//const uint STARTF_USESTDHANDLES = 0x00000100;
}



}
