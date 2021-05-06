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

using Dirigent.Common;

using CommandLine;
using CommandLine.Text;
using log4net.Appender;
using log4net;

namespace Dirigent.Common
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

		[Option( "sharedConfigFile", Required = false, Default = "", HelpText = "shared config file name." )]
		public string SharedConfigFile { get; set; } = string.Empty;

		[Option( "localConfigFile", Required = false, Default = "", HelpText = "local config file name." )]
		public string LocalConfigFile { get; set; } = string.Empty;

		[Option( "rootForRelativePaths", Required = false, Default = "", HelpText = "root folder for relative paths if used in StartupDir, FullExeName etc." )]
		public string RootForRelativePaths { get; set; } = string.Empty;

		[Option( "logFile", Required = false, Default = "", HelpText = "log file name." )]
		public string LogFile { get; set; } = string.Empty;

		[Option( "startupPlan", Required = false, Default = "", HelpText = "Plan to be started on startup." )]
		public string StartupPlan { get; set; } = string.Empty;

		[Option( "startHidden", Required = false, Default = "", HelpText = "Start with Dirigent GUI hidden in tray [0|1]." )]
		public string StartHidden { get; set; } = string.Empty;

		[Option( "isMaster", Required = false, Default = "", HelpText = "Start Master process automatically [0|1]." )]
		public string IsMaster { get; set; } = string.Empty;

		[Option( "CLIPort", Required = false, Default = 0, HelpText = "Master's Command Line Interface TCP port (passed to Master process)." )]
		public int CLIPort { get; set; }

		[Option( "mode", Required = false, Default = "", HelpText = "Mode of operation. [daemon|trayGui|remoteControlGui]." )]
		public string Mode { get; set; } = string.Empty;

		[Option( "tickPeriod", Required = false, Default = 0, HelpText = "Refresh period in msec." )]
		public int TickPeriod { get; set; }

		[Option( "parentPid", Required = false, Default = -1, HelpText = "PID of the parent (used by agent process if started from the gui process)" )]
		public int parentPid { get; set; }

		[Value( 0 )]
		public IEnumerable<string> Items { get; set; } = new List<string>();
	}

	public class AppConfig
	{
		// start with default settings
		public string SharedCfgFileName = ""; // Path.Combine(Application.StartupPath, "SharedConfig.xml");
		public string LocalCfgFileName = ""; // empty by default - we won't try to load it
		public string MachineId = System.Environment.MachineName;
		public int MasterPort = 5045;
		public int CliPort = 5050;
		public string MasterIP = "127.0.0.1";
		public string LogFileName = "";
		public string StartupPlanName = "";
		public string StartHidden = "0"; // "0" or "1"
		public string Mode = ""; // "", "agent", "master", "cli"
		public SharedConfig? SharedConfig = null;
		public LocalConfig? LocalConfig = null;
		public string RootForRelativePaths = "";
		public string IsMaster = "0"; // "1"=run the master process automatically
		public int TickPeriod = 500; // msec
		public string McastIP = "239.121.121.121";
		public string LocalIP = "0.0.0.0";
		public string McastAppStates = "0";
		public IList<string> NonOptionArgs = new List<string>();
		public int ParentPid = -1;

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
			if( Properties.Settings.Default.MachineId != "" ) MachineId = Properties.Settings.Default.MachineId;
			if( Properties.Settings.Default.MasterIP != "" ) MasterIP = Properties.Settings.Default.MasterIP;
			if( Properties.Settings.Default.McastIP != "" ) McastIP = Properties.Settings.Default.McastIP;
			if( Properties.Settings.Default.LocalIP != "" ) LocalIP = Properties.Settings.Default.LocalIP;
			if( Properties.Settings.Default.McastAppStates != "" ) McastAppStates = Properties.Settings.Default.McastAppStates;
			if( Properties.Settings.Default.MasterPort != 0 ) MasterPort = Properties.Settings.Default.MasterPort;
			if( Properties.Settings.Default.SharedConfigFile != "" ) SharedCfgFileName = Properties.Settings.Default.SharedConfigFile;
			if( Properties.Settings.Default.LocalConfigFile != "" ) LocalCfgFileName = Properties.Settings.Default.LocalConfigFile;
			if( Properties.Settings.Default.RootForRelativePaths != "" ) Mode = Properties.Settings.Default.RootForRelativePaths;
			if( Properties.Settings.Default.Mode != "" ) Mode = Properties.Settings.Default.Mode;
			if( Properties.Settings.Default.StartupPlan != "" ) StartupPlanName = Properties.Settings.Default.StartupPlan;
			if( Properties.Settings.Default.StartHidden != "" ) StartHidden = Properties.Settings.Default.StartHidden;
			if( Properties.Settings.Default.Mode != "" ) Mode = Properties.Settings.Default.Mode;
			if( Properties.Settings.Default.Mode != "" ) Mode = Properties.Settings.Default.Mode;
			if( Properties.Settings.Default.IsMaster != "" ) IsMaster = Properties.Settings.Default.IsMaster;
			if( Properties.Settings.Default.CLIPort != 0 ) CliPort = Properties.Settings.Default.CLIPort;
			if( Properties.Settings.Default.TickPeriod != 0 ) TickPeriod = Properties.Settings.Default.TickPeriod;
			if( Properties.Settings.Default.LogFile  != "" ) LogFileName = Properties.Settings.Default.LogFile;

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
				if( options.StartupPlan != "" ) StartupPlanName = options.StartupPlan;
				if( options.StartHidden != "" ) StartHidden = options.StartHidden;
				if( options.Mode != "" ) Mode = options.Mode;
				if( options.RootForRelativePaths != "" ) RootForRelativePaths = options.RootForRelativePaths;
				if( options.IsMaster != "" ) IsMaster = options.IsMaster;
				if( options.CLIPort != 0 ) CliPort = options.CLIPort;
				if( options.TickPeriod != 0 ) TickPeriod = options.TickPeriod;
				ParentPid = options.parentPid;
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
				log.DebugFormat( "Loading shared config file '{0}'", SharedCfgFileName );
				SharedConfig = new SharedXmlConfigReader( File.OpenText( SharedCfgFileName ) ).cfg;
			}

			if( LocalCfgFileName != "" )
			{
				LocalCfgFileName = Path.GetFullPath( LocalCfgFileName );
				log.DebugFormat( "Loading local config file '{0}'", LocalCfgFileName );
				LocalConfig = new LocalXmlConfigReader( File.OpenText( LocalCfgFileName ) ).cfg;
			}

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
