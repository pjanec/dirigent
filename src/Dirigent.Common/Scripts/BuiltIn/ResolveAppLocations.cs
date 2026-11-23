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

			/// <summary>
			/// Where <see cref="FileVersion"/> came from: "resource" (the version resource of the executable), or the
			/// full path of a version file next to it, whose whole text (possibly several lines) is then the version.
			/// </summary>
			public string? VersionSource;

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

			// All at once: the agent answers each launch spec on its next tick (500 ms by default), so asking
			// one app after another cost a tick per app - 11 s for a machine of 21 apps on a real site.
			// Asked together, they are all answered in the same tick.
			var entries = await Task.WhenAll( args.Apps.Select( LocateGuarded ) );
			result.Entries.AddRange( entries );

			return Tools.Serialize( result );
		}

		async Task<TEntry> LocateGuarded( AppDef app )
		{
			try
			{
				return new TEntry { Location = await Locate( app ) };
			}
			catch( Exception e )
			{
				log.Warn( $"Could not locate {app.Id}: {e.Message}" );
				return new TEntry { Error = Tools.JustFirstLine( e.Message ) };
			}
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

		public static void FillFileFacts( TPath p )
		{
			if( string.IsNullOrEmpty( p.LocalPath ) || !File.Exists( p.LocalPath ) ) return;

			p.Exists = true;
			try
			{
				var info = new FileInfo( p.LocalPath );
				p.Size = info.Length;
				p.Modified = info.LastWriteTimeUtc;

				var version = FileVersionInfo.GetVersionInfo( p.LocalPath ).FileVersion;
				if( !string.IsNullOrWhiteSpace( version ) )
				{
					p.FileVersion = version;
					p.VersionSource = "resource";
				}
			}
			catch( Exception e )
			{
				log.Debug( $"Could not read the facts of {p.LocalPath}: {e.Message}" );
			}

			// Many of our executables carry no version resource (native builds) but are delivered with
			// a text file saying what they are. Its whole text is the version - often several lines (version, commit,
			// date); a JSON answer escapes the line breaks, so a one-line CLI answer stays one line.
			if( p.FileVersion is null )
			{
				var file = FindVersionFile( Path.GetDirectoryName( p.LocalPath ) );
				if( file is not null )
				{
					try
					{
						p.FileVersion = ReadVersionFile( file );
						p.VersionSource = file;
					}
					catch( Exception e )
					{
						log.Debug( $"Could not read {file}: {e.Message}" );
					}
				}
			}
		}

		/// <summary>
		/// The names of the version files tried next to an executable, in this order; case does not matter. After them, the
		/// first (by name) of <see cref="VersionFilePrefix"/>*.txt - some builds stamp "VersionStamp.txt",
		/// others "VersionStamp-<part>.txt".
		/// </summary>
		public static readonly string[] VersionFileNames = { "version.txt", "versioninfo.txt", "versionstamp.txt" };
		public const string VersionFilePrefix = "versionstamp";

		/// <summary>No version file is longer than this; a bigger one is cut, so a mistaken match cannot flood the answer.</summary>
		const int _maxVersionFileChars = 4096;

		static string? FindVersionFile( string? folder )
		{
			if( string.IsNullOrEmpty( folder ) || !Directory.Exists( folder ) ) return null;

			// enumerated rather than File.Exists, so that "Version.TXT" is found on a case-sensitive file system too
			var present = Directory.EnumerateFiles( folder, "*.txt" )
							.ToDictionary( f => Path.GetFileName( f ), f => f, StringComparer.OrdinalIgnoreCase );

			foreach( var name in VersionFileNames )
				if( present.TryGetValue( name, out var path ) ) return path;

			return present.Keys
					.Where( n => n.StartsWith( VersionFilePrefix, StringComparison.OrdinalIgnoreCase ) )
					.OrderBy( n => n, StringComparer.OrdinalIgnoreCase )
					.Select( n => present[n] )
					.FirstOrDefault();
		}

		static string? ReadVersionFile( string path )
		{
			var text = File.ReadAllText( path );
			if( text.Length > _maxVersionFileChars ) text = text.Substring( 0, _maxVersionFileChars );
			text = text.Replace( "\r\n", "\n" ).Trim();
			return text.Length == 0 ? null : text;
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
