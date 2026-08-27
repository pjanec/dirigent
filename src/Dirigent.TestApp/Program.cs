using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dirigent.TestApp
{
	/// <summary>
	/// An application that exists only to be controlled and observed by the integration tests.
	/// Every behaviour Dirigent claims to handle gets one switch, and everything this app does
	/// is observable in a file or in its exit code - never only on the console, so that the
	/// assertions never depend on capturing output or on timing luck.
	/// </summary>
	public static class Program
	{
		const string Usage = @"Dirigent.TestApp - a controllable stand-in for a real application

  --run-forever              idle until killed (the default when nothing else is given)
  --exit-after <seconds>     terminate on its own after the given time
  --exit-code <n>            exit code to terminate with (default 0)
  --write-log <path>         append a timestamped line to this file
  --every <seconds>          how often to append (default 0.5)
  --ready-after <seconds>    delay before the readiness marker is written
  --ready-file <path>        file to create once ready
  --print-env <path>         write the whole environment to this file and continue
  --ignore-close             refuse Ctrl+C / close requests (to exercise soft kill)
  --spawn-children <n>       start n idle copies of itself (to exercise kill tree)
  --children-file <path>     write the pids of the spawned children here
  --parent-pid <pid>         give up if this process is gone (children use this)
  --help                     print this
";

		static int Main( string[] args )
		{
			var opts = Options.Parse( args );

			if( opts.ShowHelp )
			{
				Console.Write( Usage );
				return 0;
			}

			if( opts.IgnoreClose )
			{
				Console.CancelKeyPress += ( s, e ) =>
				{
					e.Cancel = true; // stay alive on purpose
					Log( "ignoring close request" );
				};
			}

			if( !string.IsNullOrEmpty( opts.PrintEnvPath ) )
			{
				WriteEnvironment( opts.PrintEnvPath! );
			}

			var children = SpawnChildren( opts.SpawnChildren, opts.ChildrenFile );

			try
			{
				return RunLoop( opts );
			}
			finally
			{
				foreach( var child in children )
				{
					try { if( !child.HasExited ) child.Kill( entireProcessTree: true ); } catch {}
				}
			}
		}

		static int RunLoop( Options opts )
		{
			var started = Stopwatch.StartNew();
			var lastLogWrite = TimeSpan.FromSeconds( -1000 );
			var lastParentCheck = TimeSpan.Zero;
			TimeSpan? parentGoneAt = null;
			bool readyWritten = string.IsNullOrEmpty( opts.ReadyFile );

			Log( $"started, pid={Environment.ProcessId}" );

			while( true )
			{
				var now = started.Elapsed;

				if( !readyWritten && now.TotalSeconds >= opts.ReadyAfterSeconds )
				{
					WriteReadyFile( opts.ReadyFile! );
					readyWritten = true;
				}

				if( !string.IsNullOrEmpty( opts.WriteLogPath )
					&& ( now - lastLogWrite ).TotalSeconds >= opts.EverySeconds )
				{
					AppendLogLine( opts.WriteLogPath! );
					lastLogWrite = now;
				}

				if( opts.ExitAfterSeconds.HasValue && now.TotalSeconds >= opts.ExitAfterSeconds.Value )
				{
					Log( $"exiting with code {opts.ExitCode}" );
					return opts.ExitCode;
				}

				// the orphan backstop: generous enough not to mask a kill that should have worked,
				// short enough that nothing of ours is still around by the next build
				if( opts.ParentPid > 0 && ( now - lastParentCheck ).TotalSeconds >= 1.0 )
				{
					lastParentCheck = now;

					if( !IsProcessAlive( opts.ParentPid ) )
					{
						if( !parentGoneAt.HasValue ) parentGoneAt = now;

						if( ( now - parentGoneAt.Value ).TotalSeconds >= 60.0 )
						{
							Log( $"parent {opts.ParentPid} has been gone for a minute, giving up" );
							return 0;
						}
					}
					else
					{
						parentGoneAt = null;
					}
				}

				Thread.Sleep( 50 );
			}
		}

		/// <param name="childrenFile">
		/// Where to write the pids of the children, one per line. Without it a test would have to ask
		/// Windows who our children are, which needs WMI for no good reason.
		/// </param>
		static List<Process> SpawnChildren( int count, string? childrenFile )
		{
			var res = new List<Process>();
			if( count <= 0 ) return res;

			var exePath = Environment.ProcessPath;
			if( string.IsNullOrEmpty( exePath ) ) return res;

			for( int i = 0; i < count; i++ )
			{
				try
				{
					// A child is told who its parent is and gives up when the parent is gone. Killing the
					// parent hard orphans the children, and an orphan nobody can identify any more would
					// sit there holding this executable until someone noticed. The grace period is long
					// enough that it cannot rescue a kill-tree test that should have failed.
					var childArgs = $"--run-forever --parent-pid {Environment.ProcessId}";
					var p = Process.Start( new ProcessStartInfo( exePath, childArgs ) { UseShellExecute = false } );
					if( p is not null ) res.Add( p );
				}
				catch( Exception ex )
				{
					Log( $"could not spawn child: {ex.Message}" );
				}
			}

			if( !string.IsNullOrEmpty( childrenFile ) )
			{
				WriteFileSafely( childrenFile!, string.Join( Environment.NewLine, res.Select( p => p.Id ) ) );
			}

			Log( $"spawned {res.Count} child(ren): {string.Join( ", ", res.Select( p => p.Id ) )}" );
			return res;
		}

		static bool IsProcessAlive( int pid )
		{
			try
			{
				using var process = Process.GetProcessById( pid );
				return !process.HasExited;
			}
			catch( ArgumentException )
			{
				return false;   // no such process
			}
		}

		static void WriteEnvironment( string path )
		{
			var sb = new StringBuilder();
			foreach( var key in Environment.GetEnvironmentVariables().Keys.Cast<object>()
						.Select( k => k?.ToString() ?? "" ).OrderBy( k => k, StringComparer.OrdinalIgnoreCase ) )
			{
				sb.AppendLine( $"{key}={Environment.GetEnvironmentVariable( key )}" );
			}
			WriteFileSafely( path, sb.ToString() );
		}

		static void WriteReadyFile( string path )
		{
			WriteFileSafely( path, $"ready {DateTime.Now:O} pid={Environment.ProcessId}{Environment.NewLine}" );
			Log( "ready" );
		}

		static void AppendLogLine( string path )
		{
			var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} pid={Environment.ProcessId} still running{Environment.NewLine}";
			try
			{
				Directory.CreateDirectory( Path.GetDirectoryName( Path.GetFullPath( path ) )! );
				File.AppendAllText( path, line );
			}
			catch( Exception ex )
			{
				Log( $"could not append to {path}: {ex.Message}" );
			}
		}

		static void WriteFileSafely( string path, string content )
		{
			try
			{
				Directory.CreateDirectory( Path.GetDirectoryName( Path.GetFullPath( path ) )! );
				File.WriteAllText( path, content );
			}
			catch( Exception ex )
			{
				Log( $"could not write {path}: {ex.Message}" );
			}
		}

		static void Log( string message )
			=> Console.WriteLine( $"[testapp] {message}" );

		class Options
		{
			public bool ShowHelp;
			public double? ExitAfterSeconds;
			public int ExitCode;
			public string? WriteLogPath;
			public double EverySeconds = 0.5;
			public double ReadyAfterSeconds;
			public string? ReadyFile;
			public string? PrintEnvPath;
			public bool IgnoreClose;
			public int SpawnChildren;
			public string? ChildrenFile;
			public int ParentPid = 0;

			public static Options Parse( string[] args )
			{
				var o = new Options();

				for( int i = 0; i < args.Length; i++ )
				{
					string Next( string name )
					{
						if( i + 1 >= args.Length )
							throw new ArgumentException( $"{name} needs a value" );
						return args[++i];
					}

					switch( args[i].ToLowerInvariant() )
					{
						case "--help":
						case "-h":
						case "/?":            o.ShowHelp = true; break;
						case "--run-forever": break; // the default behaviour anyway
						case "--exit-after":  o.ExitAfterSeconds = double.Parse( Next( "--exit-after" ), System.Globalization.CultureInfo.InvariantCulture ); break;
						case "--exit-code":   o.ExitCode = int.Parse( Next( "--exit-code" ) ); break;
						case "--write-log":   o.WriteLogPath = Next( "--write-log" ); break;
						case "--every":       o.EverySeconds = double.Parse( Next( "--every" ), System.Globalization.CultureInfo.InvariantCulture ); break;
						case "--ready-after": o.ReadyAfterSeconds = double.Parse( Next( "--ready-after" ), System.Globalization.CultureInfo.InvariantCulture ); break;
						case "--ready-file":  o.ReadyFile = Next( "--ready-file" ); break;
						case "--print-env":   o.PrintEnvPath = Next( "--print-env" ); break;
						case "--ignore-close": o.IgnoreClose = true; break;
						case "--spawn-children": o.SpawnChildren = int.Parse( Next( "--spawn-children" ) ); break;
						case "--children-file": o.ChildrenFile = Next( "--children-file" ); break;
						case "--parent-pid": o.ParentPid = int.Parse( Next( "--parent-pid" ) ); break;
						default:
							Console.WriteLine( $"[testapp] ignoring unknown argument '{args[i]}'" );
							break;
					}
				}

				return o;
			}
		}
	}
}
