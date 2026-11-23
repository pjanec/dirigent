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

		[Option( "agentStatusFolder", Required = false, Default = "", HelpText = "folder for the agent status file used for post-crash recovery. Default is %LocalAppData%\\Dirigent." )]
		public string AgentStatusFolder { get; set; } = string.Empty;

		[Option( "archiveFolder", Required = false, Default = "", HelpText = "master only: folder where download jobs of the REST API/CLI keep their archives. Default is <agentStatusFolder>\\Archives. Put it under a file share if packages span several machines." )]
		public string ArchiveFolder { get; set; } = string.Empty;

		[Option( "archiveRetentionHours", Required = false, Default = -1, HelpText = "master only: hours a finished download job is kept before it is deleted. Default 24." )]
		public int ArchiveRetentionHours { get; set; } = -1;

		[Option( "archiveMaxBytes", Required = false, Default = 0L, HelpText = "master only: total size of the download job archives above which the oldest finished ones are deleted. 0 = no limit (default)." )]
		public long ArchiveMaxBytes { get; set; }

		[Option( "serveArchives", Required = false, Default = "", HelpText = "master only: whether the web server may hand out the archives of the download jobs [0|1]. Default 1." )]
		public string ServeArchives { get; set; } = string.Empty;

		[Option( "downloadFolder", Required = false, Default = "", HelpText = "folder where downloaded file packages are placed. Default is the download folder of the user the agent runs as." )]
		public string DownloadFolder { get; set; } = string.Empty;

		[Option( "logFile", Required = false, Default = "", HelpText = "log file name." )]
		public string LogFile { get; set; } = string.Empty;

		[Option( "startupPlan", Required = false, Default = "", HelpText = "Plan to be selected on startup." )]
		public string StartupPlan { get; set; } = string.Empty;

		[Option( "startupScript", Required = false, Default = "", HelpText = "Script to be started on startup." )]
		public string StartupScript { get; set; } = string.Empty;

		[Option( "startupScriptParams", Required = false, Default = "", HelpText = "Script to be started on startup." )]
		public string StartupScriptParams { get; set; } = string.Empty;

		[Option( "startHidden", Required = false, Default = "", HelpText = "Start with Dirigent GUI hidden in tray [0|1]." )]
		public string StartHidden { get; set; } = string.Empty;

		[Option( "isMaster", Required = false, Default = "", HelpText = "Start Master process automatically [0|1]." )]
		public string IsMaster { get; set; } = string.Empty;

		[Option( "CLIPort", Required = false, Default = "", HelpText = "Command Line Interface TCP port (master only), or off for no telnet CLI. With --security on or audit the CLI is off unless a port is given." )]
		public string CLIPort { get; set; } = string.Empty;

		[Option( "security", Required = false, Default = "", HelpText = "Whether the master port is secured with TLS and the site secret [off|audit|on]. Default off. Never in the shared config." )]
		public string Security { get; set; } = string.Empty;

		[Option( "siteSecretFile", Required = false, Default = "", HelpText = "File holding the site secret (at least 32 characters). Default: site.secret in the folder of the LocalConfig.xml, then in the folder of the executable." )]
		public string SiteSecretFile { get; set; } = string.Empty;

		[Option( "tokenPublicKeyFile", Required = false, Default = "", HelpText = "File holding the public key that checks REST tokens. Default: remoting-public.pem next to the LocalConfig.xml, then next to the executable." )]
		public string TokenPublicKeyFile { get; set; } = string.Empty;

		[Option( "httpPort", Required = false, Default = 0, HelpText = "Web server port (master only, -1=no web server)." )]
		public int HttpPort { get; set; }

		[Option( "mode", Required = false, Default = "", HelpText = "Mode of operation. [daemon|trayGui|remoteControlGui]." )]
		public string Mode { get; set; } = string.Empty;

		[Option( "tickPeriod", Required = false, Default = 0, HelpText = "Refresh period in msec." )]
		public int TickPeriod { get; set; }

		[Option( "masterPeriod", Required = false, Default = 0, HelpText = "Master refresh period in msec." )]
		public int MasterTickPeriod { get; set; }

		[Option( "pid", Required = false, Default = -1, HelpText = "PID of the agent process (used by the Dirigent.Starter)" )]
		public int pid { get; set; }

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

		/// <summary>A port was named for the telnet CLI, by the command line or the .config - as opposed to the default.</summary>
		public bool CliPortExplicit = false;

		/// <summary>The CLIPort said <c>off</c>: no telnet CLI.</summary>
		public bool CliOff = false;

		/// <summary>
		/// Whether the master starts the telnet CLI. It runs any command without checking who asks, so with security on or audit it is
		/// off unless somebody named its port.
		/// </summary>
		public bool CliEnabled => !CliOff && ( CliPortExplicit || !SecurityConfigured );

		/// <summary>The telnet CLI is on although the master is secured, because its port was named: it asks nobody anything.</summary>
		public bool CliUnauthenticatedWhileSecured => CliEnabled && SecurityConfigured;

		/// <summary>Off, audit or on, as written; empty = off.</summary>
		public string Security = "";

		public string SiteSecretFile = "";
		public string TokenPublicKeyFile = "";

		/// <summary>Set up by <see cref="LoadSecurity"/>, or given in memory (the test bed). Null until then: nothing is secured.</summary>
		public SecuritySettings? SecuritySettings;

		/// <summary>Whether security is on or audit, by the text of the setting or by what was loaded.</summary>
		public bool SecurityConfigured => SecuritySettings?.Secured ?? SafeMode() != ESecurityMode.Off;

		ESecurityMode SafeMode()
		{
			try { return Dirigent.SecuritySettings.ParseMode( Security ); }
			catch( SecurityConfigException ) { return ESecurityMode.On; } // a bad value is an error at LoadSecurity; until then, the cautious reading
		}

		/// <summary>
		/// Reads the security setting and, if it is on or audit, the site secret. Every process that talks to the master calls this at start, and does not
		/// start if it throws.
		/// </summary>
		/// <exception cref="SecurityConfigException">the setting is not off|audit|on, or the secret is missing, unreadable or too short</exception>
		public SecuritySettings LoadSecurity()
		{
			SecuritySettings ??= Dirigent.SecuritySettings.Load( Dirigent.SecuritySettings.ParseMode( Security ), SiteSecretFile, TokenPublicKeyFile, LocalCfgFileName, AppContext.BaseDirectory );
			return SecuritySettings;
		}
		public int HttpPort = 8877;
		public string MasterIP = "127.0.0.1";
		public string LogFileName = "";
		public string StartupPlan = "";
		public string StartupScript = "";
		public string StartupScriptParams = ""; // params to be passed to the startup script; json token converted to script's args
		public string StartHidden = "0"; // "0" or "1"
		public string Mode = ""; // "", "agent", "master", "cli"
		//public SharedConfig? SharedConfig = null;
		//public LocalConfig? LocalConfig = null;
		public string RootForRelativePaths = "";

		/// <summary>Where to keep the agent status file. Empty = %LocalAppData%\Dirigent</summary>
		public string AgentStatusFolder = "";

		/// <summary>Where downloaded file packages are placed. Empty = the user's download folder</summary>
		public string DownloadFolder = "";

		/// <summary>Master: where download jobs keep their archives. Empty = &lt;AgentStatusFolder&gt;\Archives</summary>
		public string ArchiveFolder = "";

		/// <summary>Master: hours a finished download job is kept.</summary>
		public int ArchiveRetentionHours = 24;

		/// <summary>Master: total size of the job archives above which the oldest finished are deleted. 0 = no cap.</summary>
		public long ArchiveMaxBytes = 0;

		/// <summary>Master: may the web server hand out the archives of the download jobs.</summary>
		public string ServeArchives = "1"; // "0" or "1"

		public string IsMaster = "0"; // "1"=run the master process automatically
		public int TickPeriod = 500; // msec
		public int MasterTickPeriod = 50; // msec
		public string McastIP = "239.121.121.121";
		public string LocalIP = "0.0.0.0";
		public string McastAppStates = "0";
		public IList<string> NonOptionArgs = new List<string>();
		public int Pid = -1;
		public string GuiAppExe = "";
		public string Debug = "0";

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public bool HadErrors = false;

		/// <summary>
		/// True when the command line asked for --help or --version, which
		/// <see cref="WriteHelpOrVersion"/> then answers. The application should print nothing else
		/// and exit successfully rather than carry on and start.
		/// </summary>
		public bool HelpOrVersionRequested = false;

		/// <summary>Which of the two was asked for.</summary>
		bool _versionRequested = false;

		ParserResult<Options> _parserResult;

		public string GetUsageHelpText()
		{
			return HelpText.AutoBuild( _parserResult ).ToString();
		}

		/// <summary>
		/// Answers --help or --version on standard output. Call it when
		/// <see cref="HelpOrVersionRequested"/> is set; it prints nothing otherwise.
		/// </summary>
		/// <remarks>
		/// Printed here rather than by the parser, and on stdout rather than stderr, because a
		/// question that was asked deserves an answer on the ordinary output - a version a script
		/// cannot read without redirecting stderr is not much of a version. The parser's own writer
		/// is switched off for the same reason it has to be: it wrote the usage text to stderr on
		/// every bad command line as well, while each executable was already writing that same text
		/// itself, so one mistyped option produced the whole option list twice on two streams.
		/// </remarks>
		public void WriteHelpOrVersion()
		{
			if( !HelpOrVersionRequested ) return;

			Console.Out.WriteLine( _versionRequested ? VersionText() : GetUsageHelpText() );
		}

		/// <summary>The program and its version, as the parser's banner used to say it.</summary>
		public static string VersionText()
		{
			var asm = System.Reflection.Assembly.GetEntryAssembly();

			var name = asm?.GetName().Name ?? "Dirigent";
			var version = asm?.GetCustomAttributes( typeof( System.Reflection.AssemblyInformationalVersionAttribute ), false )
							.OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
							.FirstOrDefault()?.InformationalVersion
						?? asm?.GetName().Version?.ToString()
						?? "unknown";

			return $"{name} {version}";
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
			// note: this used to assign to Mode, which silently broke both settings
			if( Common.Properties.Settings.Default.RootForRelativePaths != "" ) RootForRelativePaths = Common.Properties.Settings.Default.RootForRelativePaths;
			if( Common.Properties.Settings.Default.Mode != "" ) Mode = Common.Properties.Settings.Default.Mode;
			if( Common.Properties.Settings.Default.StartupPlan != "" ) StartupPlan = Common.Properties.Settings.Default.StartupPlan;
			if( Common.Properties.Settings.Default.StartupScript != "" ) StartupScript = Common.Properties.Settings.Default.StartupScript;
			if( Common.Properties.Settings.Default.StartupScriptParams != "" ) StartupScriptParams = Common.Properties.Settings.Default.StartupScriptParams;
			if( Common.Properties.Settings.Default.StartHidden != "" ) StartHidden = Common.Properties.Settings.Default.StartHidden;
			if( Common.Properties.Settings.Default.IsMaster != "" ) IsMaster = Common.Properties.Settings.Default.IsMaster;
			ApplyCliPort( Common.Properties.Settings.Default.CLIPort, "the CLIPort setting" );
			if( Common.Properties.Settings.Default.Security != "" ) Security = Common.Properties.Settings.Default.Security;
			if( Common.Properties.Settings.Default.SiteSecretFile != "" ) SiteSecretFile = Common.Properties.Settings.Default.SiteSecretFile;
			if( Common.Properties.Settings.Default.TokenPublicKeyFile != "" ) TokenPublicKeyFile = Common.Properties.Settings.Default.TokenPublicKeyFile;
			if( Common.Properties.Settings.Default.HttpPort != 0 ) HttpPort = Common.Properties.Settings.Default.HttpPort;
			if( Common.Properties.Settings.Default.TickPeriod != 0 ) TickPeriod = Common.Properties.Settings.Default.TickPeriod;
			if( Common.Properties.Settings.Default.MasterTickPeriod != 0 ) MasterTickPeriod = Common.Properties.Settings.Default.MasterTickPeriod;
			if( Common.Properties.Settings.Default.LogFile  != "" ) LogFileName = Common.Properties.Settings.Default.LogFile;
			if( Common.Properties.Settings.Default.GuiAppExe != "" ) GuiAppExe = Common.Properties.Settings.Default.GuiAppExe;
			if( Common.Properties.Settings.Default.Debug != "" ) Debug = Common.Properties.Settings.Default.Debug;

			// GetCommandLineArgs() leads with this executable's own path, and the parser must not be
			// given it: a positional value is then always present, and CommandLineParser answers
			// --version and --help itself only when such an option stands alone on the line. With the
			// path there it never did, so "--version" - advertised in our own help screen - came back
			// as "Option 'version' is unknown". Dropped here rather than stripped from Items later,
			// which is what used to happen.
			// Not Parser.Default: its HelpWriter is Console.Error and it writes the whole option list
			// there for a bad command line as well as for --help, which every executable here then
			// wrote again itself. Silenced, so that what reaches the console is written once, by us,
			// on the stream that suits it - see WriteHelpOrVersion.
			var parser = new CommandLine.Parser( settings =>
			{
				settings.HelpWriter = null;
				settings.CaseSensitive = true;   // as Parser.Default has it
				settings.AutoHelp = true;
				settings.AutoVersion = true;
			} );

			_parserResult = parser.ParseArguments<Options>(
					System.Environment.GetCommandLineArgs().Skip( 1 ) );

			_parserResult.WithParsed<Options>( ( Options options ) =>
			{
				NonOptionArgs = options.Items.ToList();

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
				if( options.StartupScriptParams != "" ) StartupScriptParams = options.StartupScriptParams;
				if( options.StartHidden != "" ) StartHidden = options.StartHidden;
				if( options.Mode != "" ) Mode = options.Mode;
				if( options.RootForRelativePaths != "" ) RootForRelativePaths = options.RootForRelativePaths;
				if( options.AgentStatusFolder != "" ) AgentStatusFolder = options.AgentStatusFolder;
				if( options.DownloadFolder != "" ) DownloadFolder = options.DownloadFolder;
				if( options.ArchiveFolder != "" ) ArchiveFolder = options.ArchiveFolder;
				if( options.ArchiveRetentionHours >= 0 ) ArchiveRetentionHours = options.ArchiveRetentionHours;
				if( options.ArchiveMaxBytes != 0 ) ArchiveMaxBytes = options.ArchiveMaxBytes;
				if( options.ServeArchives != "" ) ServeArchives = options.ServeArchives;
				if( options.IsMaster != "" ) IsMaster = options.IsMaster;
				ApplyCliPort( options.CLIPort, "--CLIPort" );
				if( options.Security != "" ) Security = options.Security;
				if( options.SiteSecretFile != "" ) SiteSecretFile = options.SiteSecretFile;
				if( options.TokenPublicKeyFile != "" ) TokenPublicKeyFile = options.TokenPublicKeyFile;
				if( options.HttpPort != 0 ) HttpPort = options.HttpPort;
				if( options.TickPeriod != 0 ) TickPeriod = options.TickPeriod;
				if( options.MasterTickPeriod != 0 ) MasterTickPeriod = options.MasterTickPeriod;
				Pid = options.pid;
				if( options.GuiAppExe != "" ) GuiAppExe = options.GuiAppExe;
				if( options.Debug != "" ) Debug = options.Debug;
			} )
			.WithNotParsed<Options>( ( errList ) =>
			{
				// CommandLineParser reports --help and --version through this same path. That is a
				// request answered, not a mistake on the command line, so it must not be logged as
				// an error or exited non-zero - and, the writer being off, somebody has to print it:
				// WriteHelpOrVersion, called by each executable.
				_versionRequested = errList.Any( e => e.Tag == CommandLine.ErrorType.VersionRequestedError );

				HelpOrVersionRequested = _versionRequested
						|| errList.Any( e => e.Tag == CommandLine.ErrorType.HelpRequestedError
										  || e.Tag == CommandLine.ErrorType.HelpVerbRequestedError );

				HadErrors = !HelpOrVersionRequested;
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

		/// <summary>Takes the text of the CLIPort option or setting: a port, 0 for the default, or off.</summary>
		void ApplyCliPort( string? text, string where )
		{
			if( !CliPortSetting.TryParse( text, out var kind, out var port ) )
			{
				log.Error( $"{where}: '{text}' is neither a port number nor 'off'." );
				HadErrors = true;
				return;
			}

			switch( kind )
			{
				case CliPortSetting.EKind.Off: CliOff = true; CliPortExplicit = false; break;
				case CliPortSetting.EKind.Port: CliOff = false; CliPortExplicit = true; CliPort = port; break;
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
