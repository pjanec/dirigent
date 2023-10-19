/*
 * WARNING!!!
 *
 * This file is included to multiple projects (Dirigent.Agent, Dirigent.Gui..) that all accept same command line arguments.
 * It is NOT compiled into the Dirigent.Common library at all (it is excluded from Dirigent.Common project).
 * It is just stored among Dirigent.Common source files to exist in a single copy in the sources, avoiding unnecessary duplicities...
 *
 * Same applies to Properties/Settings.Designer.cs and Settings.settings files.
 * 
 * The defaults in those files must work for all projects sharing these file.
 * 
 * Settings that is different across apps needs to be stored only in App.config files (they are specific to each application, not shared.)
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using CommandLine;
using CommandLine.Text;
using log4net.Appender;
using log4net;

namespace Dirigent
{
	// Define a class to receive parsed values
	class Options
	{
		[Option( "masterPort", Required = false, Default = 0, HelpText = "Master's TCP port." )]
		public int MasterPort { get; set; }

		[Option( "masterIp", Required = false, Default = "", HelpText = "Master's IP address." )]
		public string MasterIP { get; set; } = string.Empty;

		[Option( "mcastIp", Required = false, Default = "", HelpText = "Multicast IP address." )]
		public string McastIP { get; set; } = string.Empty;

		[Option( "localIp", Required = false, Default = "", HelpText = "Local addapter IP address to bind to when multicasting." )]
		public string LocalIP { get; set; } = string.Empty;

		[Option( "mcastAppStates", Required = false, Default = "", HelpText = "Use multical for sharing app states among agents." )]
		public string McastAppStates { get; set; } = string.Empty;

		[Option( "machineId", Required = false, Default = "", HelpText = "Machine Id." )]
		public string MachineId { get; set; } = string.Empty;

		[Option( "clientId", Required = false, Default = "", HelpText = "Unique Id of the network client. Used just for GUIs" )]
		public string ClientId { get; set; } = string.Empty;

		[Option( "sharedConfigFile", Required = false, Default = "", HelpText = "shared config file name." )]
		public string SharedConfigFile { get; set; } = string.Empty;

		[Option( "localConfigFile", Required = false, Default = "", HelpText = "local config file name." )]
		public string LocalConfigFile { get; set; } = string.Empty;

		[Option( "rootForRelativePaths", Required = false, Default = "", HelpText = "root folder for relative paths if used in StartupDir, FullExeName etc." )]
		public string RootForRelativePaths { get; set; } = string.Empty;

		[Option( "logFile", Required = false, Default = "", HelpText = "log file name." )]
		public string LogFile { get; set; } = string.Empty;

		[Option( "startupPlan", Required = false, Default = "", HelpText = "Plan to be selected on startup." )]
		public string StartupPlan { get; set; } = string.Empty;

		[Option( "startupScript", Required = false, Default = "", HelpText = "Script to be started on startup." )]
		public string StartupScript { get; set; } = string.Empty;

		[Option( "startHidden", Required = false, Default = "", HelpText = "Start with Dirigent GUI hidden in tray [0|1]." )]
		public string StartHidden { get; set; } = string.Empty;

		[Option( "isMaster", Required = false, Default = "", HelpText = "Start Master process automatically [0|1]." )]
		public string IsMaster { get; set; } = string.Empty;

		[Option( "CLIPort", Required = false, Default = 0, HelpText = "Command Line Interface TCP port (master only)." )]
		public int CLIPort { get; set; }

		[Option( "httpPort", Required = false, Default = 0, HelpText = "Web server port (master only, -1=no web server)." )]
		public int HttpPort { get; set; }

		[Option( "mode", Required = false, Default = "", HelpText = "Mode of operation. [daemon|trayGui|remoteControlGui]." )]
		public string Mode { get; set; } = string.Empty;

		[Option( "tickPeriod", Required = false, Default = 0, HelpText = "Refresh period in msec." )]
		public int TickPeriod { get; set; }

		[Option( "masterPeriod", Required = false, Default = 0, HelpText = "Master refresh period in msec." )]
		public int MasterTickPeriod { get; set; }

		[Option( "parentPid", Required = false, Default = -1, HelpText = "PID of the parent (used by agent process if started from the gui process)" )]
		public int parentPid { get; set; }

		[Option( "guiAppExe", Required = false, Default = "", HelpText = "Executable for GUI" )]
		public string GuiAppExe { get; set; } = string.Empty;

		[Option( "debug", Required = false, Default = "", HelpText = "do not catch exceptions, let the debugger to break in [0|1]." )]
		public string Debug { get; set; } = string.Empty;

		[Value( 0 )]
		public IEnumerable<string> Items { get; set; } = new List<string>();
	}

	public class AppConfig
	{
		// start with default settings
		public string SharedCfgFileName = ""; // Path.Combine(Application.StartupPath, "SharedConfig.xml");
		public string LocalCfgFileName = ""; // empty by default - we won't try to load it
		public string MachineId = System.Environment.MachineName;
		public string ClientId = "";
		public int MasterPort = 5045;
		public int CliPort = 5050;
		public int HttpPort = 8877;
		public string MasterIP = "127.0.0.1";
		public string LogFileName = "";
		public string StartupPlan = "";
		public string StartupScript = "";
		public string StartHidden = "0"; // "0" or "1"
		public string Mode = ""; // "", "agent", "master", "cli"
		//public SharedConfig? SharedConfig = null;
		//public LocalConfig? LocalConfig = null;
		public string RootForRelativePaths = "";
		public string IsMaster = "0"; // "1"=run the master process automatically
		public int TickPeriod = 500; // msec
		public int MasterTickPeriod = 50; // msec
		public string McastIP = "239.121.121.121";
		public string LocalIP = "0.0.0.0";
		public string McastAppStates = "0";
		public IList<string> NonOptionArgs = new List<string>();
		public int ParentPid = -1;
		public string GuiAppExe = "";
		public string Debug = "0";

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public bool HadErrors = false;

		ParserResult<Options> _parserResult;

		public string GetUsageHelpText()
		{
			return HelpText.AutoBuild( _parserResult ).ToString();
		}

		Options options = new Options();

		public AppConfig()
		{
			// overwrite with application config
			if( Common.Properties.Settings.Default.ClientId != "" ) ClientId = Common.Properties.Settings.Default.ClientId;
			if( Common.Properties.Settings.Default.MachineId != "" ) MachineId = Common.Properties.Settings.Default.MachineId;
			if( Common.Properties.Settings.Default.MasterIP != "" ) MasterIP = Common.Properties.Settings.Default.MasterIP;
			if( Common.Properties.Settings.Default.McastIP != "" ) McastIP = Common.Properties.Settings.Default.McastIP;
			if( Common.Properties.Settings.Default.LocalIP != "" ) LocalIP = Common.Properties.Settings.Default.LocalIP;
			if( Common.Properties.Settings.Default.McastAppStates != "" ) McastAppStates = Common.Properties.Settings.Default.McastAppStates;
			if( Common.Properties.Settings.Default.MasterPort != 0 ) MasterPort = Common.Properties.Settings.Default.MasterPort;
			if( Common.Properties.Settings.Default.SharedConfigFile != "" ) SharedCfgFileName = Common.Properties.Settings.Default.SharedConfigFile;
			if( Common.Properties.Settings.Default.LocalConfigFile != "" ) LocalCfgFileName = Common.Properties.Settings.Default.LocalConfigFile;
			if( Common.Properties.Settings.Default.RootForRelativePaths != "" ) Mode = Common.Properties.Settings.Default.RootForRelativePaths;
			if( Common.Properties.Settings.Default.Mode != "" ) Mode = Common.Properties.Settings.Default.Mode;
			if( Common.Properties.Settings.Default.StartupPlan != "" ) StartupPlan = Common.Properties.Settings.Default.StartupPlan;
			if( Common.Properties.Settings.Default.StartupScript != "" ) StartupScript = Common.Properties.Settings.Default.StartupScript;
			if( Common.Properties.Settings.Default.StartHidden != "" ) StartHidden = Common.Properties.Settings.Default.StartHidden;
			if( Common.Properties.Settings.Default.IsMaster != "" ) IsMaster = Common.Properties.Settings.Default.IsMaster;
			if( Common.Properties.Settings.Default.CLIPort != 0 ) CliPort = Common.Properties.Settings.Default.CLIPort;
			if( Common.Properties.Settings.Default.HttpPort != 0 ) HttpPort = Common.Properties.Settings.Default.HttpPort;
			if( Common.Properties.Settings.Default.TickPeriod != 0 ) TickPeriod = Common.Properties.Settings.Default.TickPeriod;
			if( Common.Properties.Settings.Default.MasterTickPeriod != 0 ) MasterTickPeriod = Common.Properties.Settings.Default.MasterTickPeriod;
			if( Common.Properties.Settings.Default.LogFile  != "" ) LogFileName = Common.Properties.Settings.Default.LogFile;
			if( Common.Properties.Settings.Default.GuiAppExe != "" ) GuiAppExe = Common.Properties.Settings.Default.GuiAppExe;
			if( Common.Properties.Settings.Default.Debug != "" ) Debug = Common.Properties.Settings.Default.Debug;

			_parserResult = CommandLine.Parser.Default.ParseArguments<Options>( System.Environment.GetCommandLineArgs() );

			_parserResult.WithParsed<Options>( ( Options options ) =>
			{
				NonOptionArgs = options.Items.ToList().GetRange( 1, options.Items.Count() - 1 ); // strip the executable name

				if( options.MachineId != "" ) MachineId = options.MachineId;
				if( options.MasterIP != "" ) MasterIP = options.MasterIP;
				if( options.McastIP != "" ) McastIP = options.McastIP;
				if( options.McastAppStates != "" ) McastAppStates = options.McastAppStates;
				if( options.LocalIP != "" ) LocalIP = options.LocalIP;
				if( options.MasterPort != 0 ) MasterPort = options.MasterPort;
				if( options.SharedConfigFile != "" ) SharedCfgFileName = options.SharedConfigFile;
				if( options.LocalConfigFile != "" ) LocalCfgFileName = options.LocalConfigFile;
				if( options.LogFile != "" ) LogFileName = options.LogFile;
				if( options.StartupPlan != "" ) StartupPlan = options.StartupPlan;
				if( options.StartupScript != "" ) StartupScript = options.StartupScript;
				if( options.StartHidden != "" ) StartHidden = options.StartHidden;
				if( options.Mode != "" ) Mode = options.Mode;
				if( options.RootForRelativePaths != "" ) RootForRelativePaths = options.RootForRelativePaths;
				if( options.IsMaster != "" ) IsMaster = options.IsMaster;
				if( options.CLIPort != 0 ) CliPort = options.CLIPort;
				if( options.HttpPort != 0 ) HttpPort = options.HttpPort;
				if( options.TickPeriod != 0 ) TickPeriod = options.TickPeriod;
				if( options.MasterTickPeriod != 0 ) MasterTickPeriod = options.MasterTickPeriod;
				ParentPid = options.parentPid;
				if( options.GuiAppExe != "" ) GuiAppExe = options.GuiAppExe;
				if( options.Debug != "" ) Debug = options.Debug;
			} )
			.WithNotParsed<Options>( ( errList ) =>
			{
				HadErrors = true;
			} );


			if( LogFileName != "" )
			{
				SetLogFileName( Path.GetFullPath( LogFileName ) );
			}

			if( SharedCfgFileName != "" )
			{
				SharedCfgFileName = Path.GetFullPath( SharedCfgFileName );
			}

			if( LocalCfgFileName != "" )
			{
				LocalCfgFileName = Path.GetFullPath( LocalCfgFileName );
			}

			//if( LocalCfgFileName != "" )
			//{
			//	LocalCfgFileName = Path.GetFullPath( LocalCfgFileName );
			//	log.DebugFormat( "Loading local config file '{0}'", LocalCfgFileName );
			//	LocalConfig = new LocalXmlConfigReader( File.OpenText( LocalCfgFileName ) ).cfg;
			//}

			// if root is empty and we know the shared config path, use the shared config path
			if( string.IsNullOrEmpty( RootForRelativePaths ) )
			{
				if( !string.IsNullOrEmpty( SharedCfgFileName ) )
				{
					RootForRelativePaths = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath( SharedCfgFileName ) ) ?? string.Empty;
				}
			}


		}

		static void SetLogFileName( string newName )
		{
			log4net.Repository.Hierarchy.Hierarchy h = ( log4net.Repository.Hierarchy.Hierarchy )LogManager.GetRepository();
			foreach( IAppender a in h.Root.Appenders )
			{
				if( a is FileAppender )
				{
					FileAppender fa = ( FileAppender )a;
					fa.File = newName;
					fa.ActivateOptions();
					break;
				}
			}
		}

	}
}
