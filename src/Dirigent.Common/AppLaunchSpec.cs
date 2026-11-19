using System;
using System.Collections.Generic;
using System.IO;

namespace Dirigent
{
	/// <summary>What kind of thing an app's launch runs.</summary>
	public enum EExeType
	{
		Executable,   // normal executable startable via Process.Start
		DirigentCmd   // dirigent command(s) in text form
	}

	/// <summary>The exe part of a launch, as <see cref="AppLaunchSpec.ParseExe"/> works it out.</summary>
	public struct ParsedExe
	{
		public EExeType ExeType;
		public string Path;
		public string CmdLine;

		/// <summary>The reserved name used as the exe, without the brackets and in lower case ("cmd.file"); null for a plain exe.</summary>
		public string? ReservedName;
	}

	/// <summary>
	/// How an application is launched: the variables its paths and command line are expanded with, the
	/// program, the command line and the working folder that result. The one piece of code that says
	/// so - the <see cref="Launcher"/> starts the process from it, and whoever wants to know where an
	/// app's binary is asks the same thing.
	/// </summary>
	/// <remarks>
	/// Nothing here touches the file system, the network or the process. Moved out of the launcher
	/// unchanged; keep it that way, a launch that differs from what was reported is the worst kind of
	/// surprise.
	/// </remarks>
	public class AppLaunchSpec
	{
		readonly AppDef _appDef;
		readonly string _relativePathsRoot;

		/// <summary>Exported to the process environment; also usable for expansion.</summary>
		public Dictionary<string, string> PublishedVars { get; }

		/// <summary>The variables used for expansion of paths and command lines; contains the published ones.</summary>
		public Dictionary<string, string> ExpansionVars { get; }

		/// <param name="internalExpansionVars">the globally defined internal variables (SharedContext.ExpansionVars)</param>
		/// <param name="extraVars">variables given when the app is started; null if none</param>
		/// <param name="masterIP">address of the Dirigent master</param>
		/// <param name="relativePathsRoot">the folder relative paths are taken from</param>
		public AppLaunchSpec( AppDef appDef, Dictionary<string, string> internalExpansionVars, Dictionary<string, string>? extraVars, string masterIP, string relativePathsRoot )
		{
			_appDef = appDef;
			_relativePathsRoot = relativePathsRoot;

			var planName = appDef.PlanName;

			PublishedVars = extraVars is null ? new() : new( extraVars );   // we modify the PublishedVars dictionary later => copy necessary!

			// set environment variables here so we can use them when expanding process path/args/cwd
			// the variables of the file paths start from this very set - see FileRegistry.BuildLocalVars
			Tools.AddOrUpdateVar( PublishedVars, "DIRIGENT_PLAN", planName );
			Tools.AddOrUpdateVar( PublishedVars, "DIRIGENT_MACHINEID", appDef.Id.MachineId );
			Tools.AddOrUpdateVar( PublishedVars, "DIRIGENT_APPID", appDef.Id.AppId );
			Tools.AddOrUpdateVar( PublishedVars, "DIRIGENT_MASTER_IP", masterIP );

			ExpansionVars = BuildVars( appDef, internalExpansionVars, PublishedVars );

			Tools.AddOrUpdateVar( ExpansionVars, "MACHINE_ID", appDef.Id.MachineId );
			// note: we are missing MACHINE_IP here - which one of our network interfaces to use? Not clear. Makes sense only for remote machines.
			Tools.AddOrUpdateVar( ExpansionVars, "APP_ID", appDef.Id.AppId );
			Tools.AddOrUpdateVar( ExpansionVars, "APP_BINDIR", Tools.ExpandEnvAndInternalVars( Path.GetDirectoryName( appDef.ExeFullPath )!, appDef.EnvVarsToSet ) );
			Tools.AddOrUpdateVar( ExpansionVars, "APP_STARTUPDIR", Tools.ExpandEnvAndInternalVars( appDef.StartupDir, appDef.EnvVarsToSet ) );
			Tools.AddOrUpdateVar( ExpansionVars, "APP_STARTUPDIR_NAME", LastSegment( Tools.ExpandEnvAndInternalVars( appDef.StartupDir, appDef.EnvVarsToSet ) ) );
		}

		/// <summary>
		/// The last folder name of a path, trailing separators ignored; empty for an empty path. What a
		/// working folder like <c>..\RenderHost-3801</c> is called.
		/// </summary>
		public static string LastSegment( string path )
		{
			var trimmed = path.TrimEnd( '\\', '/' );
			return trimmed.Length == 0 ? string.Empty : Path.GetFileName( trimmed );
		}

		static Dictionary<string, string> BuildVars( AppDef appDef, Dictionary<string, string> internalVars, Dictionary<string, string> extraVars )
		{
			// start with externally defined (global) internal vars
			var res = new Dictionary<string, string>( internalVars );

			// add the local variables from appdef
			foreach( var kv in appDef.LocalVarsToSet )
			{
				Tools.AddOrUpdateVar( res, kv.Key, kv.Value );
			}

			// process explicitly specified variables
			foreach( var kv in extraVars )
			{
				Tools.AddOrUpdateVar( res, kv.Key, kv.Value );
			}

			return res;
		}

		public string ExpandVars( String str )
		{
			return Tools.ExpandEnvAndInternalVars( str, ExpansionVars );
		}

		public string BuildAbsolutePath( string anyPath )
		{
			return PathUtils.BuildAbsolutePath( anyPath, _relativePathsRoot );
		}

		static string GetCmdExePath()
		{
			return Environment.GetEnvironmentVariable( "ComSpec" ) ?? string.Empty;
		}

		static string GetPowershellExePath()
		{
			return Environment.ExpandEnvironmentVariables( @"%windir%\system32\WindowsPowerShell\v1.0\powershell.exe" ) ?? string.Empty;
		}

		public ParsedExe ParseExe()
		{
			ParsedExe pe = new ParsedExe();

			// resolve reserved exe names
			switch( _appDef.ExeFullPath.ToLower() )
			{
				// just cmd shell
				case "[cmd]":
					pe.ExeType = EExeType.Executable;
					pe.ReservedName = "cmd";
					pe.Path = GetCmdExePath();
					pe.CmdLine = ExpandVars( _appDef.CmdLineArgs );
					break;

				case "[cmd.file]":
				case "[cmd.command]":
					pe.ExeType = EExeType.Executable;
					pe.ReservedName = _appDef.ExeFullPath.ToLower().Trim( '[', ']' );
					pe.Path = GetCmdExePath();
					pe.CmdLine = "/c " + ExpandVars( _appDef.CmdLineArgs );
					break;

				// just powershell with any command line
				case "[powershell]":
					pe.ExeType = EExeType.Executable;
					pe.ReservedName = "powershell";
					pe.Path = GetPowershellExePath();
					pe.CmdLine = ExpandVars( _appDef.CmdLineArgs );
					break;

				// powershell script file specified in appDef.CmdLineArgs;
				case "[powershell.file]":
					pe.ExeType = EExeType.Executable;
					pe.ReservedName = "powershell.file";
					pe.Path = GetPowershellExePath();
					pe.CmdLine = "-executionpolicy bypass -file " + ExpandVars( _appDef.CmdLineArgs );
					break;

				// powershell command specified in CmdLineArgs
				case "[powershell.command]":
					pe.ExeType = EExeType.Executable;
					pe.ReservedName = "powershell.command";
					pe.Path = GetPowershellExePath();
					pe.CmdLine = "-executionpolicy bypass -command \"" + ExpandVars( _appDef.CmdLineArgs ) + "\"";
					break;


				// dirigent.AgentCmd command line
				case ReservedExeNames.DirigentCommand:
					pe.ExeType = EExeType.DirigentCmd;
					pe.ReservedName = "dirigent.command";
					pe.Path = string.Empty;
					pe.CmdLine = ExpandVars( _appDef.CmdLineArgs );
					break;

				default:
					pe.ExeType = EExeType.Executable;
					pe.Path = ExpandVars( _appDef.ExeFullPath );
					pe.CmdLine = ExpandVars( _appDef.CmdLineArgs );
					break;
			}

			return pe;
		}

		/// <summary>The program the process is started from: the parsed exe path, made absolute.</summary>
		public string GetFileName( ParsedExe pe ) => BuildAbsolutePath( pe.Path );

		/// <summary>The working folder of the process, or null when the app has none declared.</summary>
		public string? GetWorkingDirectory()
		{
			if( _appDef.StartupDir == null ) return null;

			var dir = ExpandVars( _appDef.StartupDir );
			return BuildAbsolutePath( dir );
		}
	}
}
