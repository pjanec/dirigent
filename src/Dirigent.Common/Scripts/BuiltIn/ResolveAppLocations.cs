using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Dirigent;

namespace Dirigent.Scripts.BuiltIn
{
	/// <summary>
	/// Says where the binaries of applications are, on the machine this script runs on - the agent that
	/// would launch them.
	/// </summary>
	/// <remarks>
	/// Batched, like ResolveVfsPath: the master sends all the apps of a machine in one call and gets one
	/// answer per app, a failure being reported in its own slot. What the launch would be - program,
	/// command line, working folder - is not worked out here but asked of the agent
	/// (<see cref="IDirigAsync.ResolveAppLaunchSpecAsync"/>), which answers with the very code the launcher
	/// starts the process from. What is added here is only what the launcher does not care about: whether
	/// the files are there, and how big, how old and which version they are.
	/// </remarks>
	public class ResolveAppLocations : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/ResolveAppLocations.cs";

		public class TArgs
		{
			/// <summary>The apps, all of this machine; the definition to resolve is the caller's choice (default or a plan's).</summary>
			public List<AppDef> Apps = new();

			public override string ToString() => $"{Apps.Count} app(s)";
		}

		/// <summary>What is found of one path.</summary>
		public class TPath
		{
			public string Declared = string.Empty;
			public string? LocalPath;
			public bool Exists;
			public long? Size;
			public DateTime? Modified;
			public string? FileVersion;

			/// <summary>The program was not where the launcher would look and was found on the agent's PATH instead.</summary>
			public bool FoundOnPath;
		}

		public class TLocation
		{
			/// <summary>"process", a reserved exe name without brackets ("cmd", "powershell.file", ...) or "dirigentCommand".</summary>
			public string Kind = "process";
			public TPath Exe = new();
			public TPath StartupDir = new();
			public string CmdLineDeclared = string.Empty;
			public string CmdLineExpanded = string.Empty;
		}

		/// <summary>One slot per app, in the order asked.</summary>
		public class TEntry
		{
			public TLocation? Location;
			public string? Error;
		}

		public class TResult
		{
			public List<TEntry> Entries = new();
		}

		protected async override Task<string?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args ) ?? throw new NullReferenceException( "Args == null" );
			var result = new TResult();

			foreach( var app in args.Apps )
			{
				try
				{
					result.Entries.Add( new TEntry { Location = await Locate( app ) } );
				}
				catch( Exception e )
				{
					log.Warn( $"Could not locate {app.Id}: {e.Message}" );
					result.Entries.Add( new TEntry { Error = Tools.JustFirstLine( e.Message ) } );
				}
			}

			return Tools.Serialize( result );
		}

		async Task<TLocation> Locate( AppDef app )
		{
			var spec = await Dirig.ResolveAppLaunchSpecAsync( app )
						?? throw new Exception( "This client cannot say how an application is launched." );

			var pe = spec.ParseExe();

			var loc = new TLocation()
			{
				Kind = pe.ExeType == EExeType.DirigentCmd ? "dirigentCommand" : ( pe.ReservedName ?? "process" ),
				CmdLineDeclared = app.CmdLineArgs,
				CmdLineExpanded = pe.CmdLine,
			};
			loc.Exe.Declared = app.ExeFullPath;
			loc.StartupDir.Declared = app.StartupDir;

			// a Dirigent command runs no program, in no folder
			if( pe.ExeType == EExeType.DirigentCmd )
				return loc;

			var file = spec.GetFileName( pe );
			loc.Exe.LocalPath = file;

			// A bare name is combined with the root of relative paths by the launcher, and so it is here. Only
			// if that finds nothing is the PATH of this machine consulted - to report where such a program
			// would be, not because the launcher would find it there.
			var bare = pe.ReservedName is null && pe.Path.Length > 0
					&& pe.Path.IndexOfAny( new[] { (char) 92, '/' } ) < 0 && !Path.IsPathRooted( pe.Path );

			if( !File.Exists( file ) && bare )
			{
				var onPath = FindOnPath( pe.Path );
				if( onPath is not null )
				{
					loc.Exe.LocalPath = onPath;
					loc.Exe.FoundOnPath = true;
				}
				else
				{
					loc.Exe.LocalPath = pe.Path; // as written
				}
			}

			FillFileFacts( loc.Exe );

			var dir = spec.GetWorkingDirectory();
			loc.StartupDir.LocalPath = dir;
			loc.StartupDir.Exists = !string.IsNullOrEmpty( dir ) && Directory.Exists( dir );

			return loc;
		}

		static void FillFileFacts( TPath p )
		{
			if( string.IsNullOrEmpty( p.LocalPath ) || !File.Exists( p.LocalPath ) ) return;

			p.Exists = true;
			try
			{
				var info = new FileInfo( p.LocalPath );
				p.Size = info.Length;
				p.Modified = info.LastWriteTimeUtc;

				var version = FileVersionInfo.GetVersionInfo( p.LocalPath ).FileVersion;
				p.FileVersion = string.IsNullOrWhiteSpace( version ) ? null : version;
			}
			catch( Exception e )
			{
				log.Debug( $"Could not read the facts of {p.LocalPath}: {e.Message}" );
			}
		}

		/// <summary>The first match of a bare program name in the PATH, trying the PATHEXT extensions when it has none.</summary>
		static string? FindOnPath( string name )
		{
			var extensions = ( Environment.GetEnvironmentVariable( "PATHEXT" ) ?? ".COM;.EXE;.BAT;.CMD" )
								.Split( new[] { ';' }, StringSplitOptions.RemoveEmptyEntries );

			var candidates = Path.HasExtension( name )
								? new[] { name }
								: new[] { name }.Concat( extensions.Select( e => name + e ) ).ToArray();

			foreach( var dir in ( Environment.GetEnvironmentVariable( "PATH" ) ?? "" ).Split( new[] { ';' }, StringSplitOptions.RemoveEmptyEntries ) )
			{
				foreach( var candidate in candidates )
				{
					try
					{
						var full = Path.Combine( dir.Trim().Trim( '"' ), candidate );
						if( File.Exists( full ) ) return full;
					}
					catch( Exception ) { }
				}
			}

			return null;
		}
	}
}
