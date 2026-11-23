using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Dirigent.Crashes
{
	/// <summary>
	/// What reading crashes needs of the machine it runs on: the event log, the file system, the registry. All
	/// of it replaceable, so that a test does not depend on the real log (see <c>QueryCrashes.EnvironmentFactory</c>).
	/// </summary>
	public class CrashEnvironment
	{
		/// <summary>The event log; null where there is none.</summary>
		public ICrashEventSource? Source { get; set; }

		public bool IsSupported => Source is not null;

		public Func<string, long?> FileSize { get; set; } = CrashFacts.FileSize;
		public Func<string, IReadOnlyList<string>> LocalDumpFolders { get; set; } = CrashFacts.LocalDumpFolders;
		public Func<string, DateTime, string?> FindMiniDump { get; set; } = CrashFacts.FindMiniDump;
		public string WindowsDir { get; set; } = Environment.GetFolderPath( Environment.SpecialFolder.Windows );

		/// <summary>Dirigent's own programs (full paths).</summary>
		public Func<IEnumerable<string>> OwnExes { get; set; } = CrashFacts.OwnExes;

		/// <summary>The versions of an exe and the DLLs beside it.</summary>
		public Func<string, CrashVersions?> ReadVersions { get; set; } = CrashFacts.ReadVersions;

		public Func<CrashMachineInfo> ReadMachineInfo { get; set; } = CrashFacts.ReadMachineInfo;

		public static CrashEnvironment Real()
		{
			var env = new CrashEnvironment();
#if Windows
			if( OperatingSystem.IsWindows() ) env.Source = new WindowsCrashEventSource();
#endif
			return env;
		}

		public CrashContext MakeContext( string machine, CrashAppTable table, DateTime nowUtc )
			=> new CrashContext
			{
				Machine = machine,
				Table = table,
				FileSize = FileSize,
				LocalDumpFolders = LocalDumpFolders,
				FindMiniDump = t => FindMiniDump( WindowsDir, t ),
				WindowsDir = WindowsDir,
			};
	}

	/// <summary>The real answers to what <see cref="CrashEnvironment"/> asks; read-only.</summary>
	public static class CrashFacts
	{
		public static long? FileSize( string path )
		{
			try
			{
				var f = new FileInfo( path );
				return f.Exists ? f.Length : null;
			}
			catch( Exception )
			{
				return null;
			}
		}

		const string LocalDumpsKey = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps";

		/// <summary>The folders WER LocalDumps would write the dump of an exe to; empty when LocalDumps is not set at all.</summary>
		public static IReadOnlyList<string> LocalDumpFolders( string exeName )
		{
			var res = new List<string>();
			if( !OperatingSystem.IsWindows() ) return res;

			try
			{
				using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey( Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64 );
				using var global = baseKey.OpenSubKey( LocalDumpsKey );
				if( global is null ) return res;

				using var own = global.OpenSubKey( exeName );
				if( own?.GetValue( "DumpFolder" ) is string ownFolder && ownFolder.Length > 0 )
					res.Add( Environment.ExpandEnvironmentVariables( ownFolder ) );

				var folder = global.GetValue( "DumpFolder" ) as string;
				var globalFolder = Environment.ExpandEnvironmentVariables( string.IsNullOrEmpty( folder ) ? @"%LOCALAPPDATA%\CrashDumps" : folder );
				if( !res.Contains( globalFolder, StringComparer.OrdinalIgnoreCase ) ) res.Add( globalFolder );
			}
			catch( Exception )
			{
				// no registry, no folders
			}

			return res;
		}

		/// <summary>The minidump of Windows\Minidump written nearest to the time, within a quarter of an hour.</summary>
		public static string? FindMiniDump( string windowsDir, DateTime utc )
		{
			try
			{
				var dir = Path.Combine( windowsDir, "Minidump" );
				if( !Directory.Exists( dir ) ) return null;

				return new DirectoryInfo( dir ).GetFiles( "*.dmp" )
					.Where( f => ( f.LastWriteTimeUtc - utc ).Duration() <= TimeSpan.FromMinutes( 15 ) )
					.OrderBy( f => ( f.LastWriteTimeUtc - utc ).Duration() )
					.Select( f => f.FullName ).FirstOrDefault();
			}
			catch( Exception )
			{
				return null;
			}
		}

		/// <summary>This process, and the programs Dirigent ships beside it.</summary>
		public static IEnumerable<string> OwnExes()
		{
			var res = new List<string>();
			try
			{
				if( !string.IsNullOrEmpty( Environment.ProcessPath ) ) res.Add( Environment.ProcessPath );
				res.AddRange( Directory.GetFiles( AppContext.BaseDirectory, "Dirigent*.exe" ) );
			}
			catch( Exception )
			{
			}
			return res.Distinct( StringComparer.OrdinalIgnoreCase );
		}

		const int _maxFiles = 500;

		public static CrashVersions? ReadVersions( string exePath )
		{
			try
			{
				if( !File.Exists( exePath ) ) return null;

				var vi = FileVersionInfo.GetVersionInfo( exePath );
				var res = new CrashVersions { exeFileVersion = vi.FileVersion, exeProductVersion = vi.ProductVersion };

				var dir = Path.GetDirectoryName( exePath );
				if( dir is not null )
				{
					foreach( var f in new DirectoryInfo( dir ).GetFiles( "*.dll" ).OrderBy( f => f.Name, StringComparer.OrdinalIgnoreCase ).Take( _maxFiles ) )
					{
						string? version = null;
						try { version = FileVersionInfo.GetVersionInfo( f.FullName ).FileVersion; } catch( Exception ) { }
						res.files.Add( new CrashFileVersion { name = f.Name, version = version, size = f.Length, modified = f.LastWriteTimeUtc } );
					}
				}
				return res;
			}
			catch( Exception )
			{
				return null;
			}
		}

		[StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto )]
		class MemoryStatusEx
		{
			public uint dwLength = (uint) Marshal.SizeOf( typeof( MemoryStatusEx ) );
			public uint dwMemoryLoad;
			public ulong ullTotalPhys;
			public ulong ullAvailPhys;
			public ulong ullTotalPageFile;
			public ulong ullAvailPageFile;
			public ulong ullTotalVirtual;
			public ulong ullAvailVirtual;
			public ulong ullAvailExtendedVirtual;
		}

		[DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		static extern bool GlobalMemoryStatusEx( [In, Out] MemoryStatusEx lpBuffer );

		const string GpuClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

		/// <summary>OS, uptime, CPU, RAM, disks and graphics adapters - from the registry and the framework, no WMI.</summary>
		public static CrashMachineInfo ReadMachineInfo()
		{
			var info = new CrashMachineInfo { uptimeSeconds = Math.Round( Environment.TickCount64 / 1000.0, 0 ) };
			if( !OperatingSystem.IsWindows() ) return info;

			try
			{
				using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey( Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64 );

				using( var nt = baseKey.OpenSubKey( @"SOFTWARE\Microsoft\Windows NT\CurrentVersion" ) )
				{
					if( nt is not null )
					{
						var product = nt.GetValue( "ProductName" ) as string;
						var display = nt.GetValue( "DisplayVersion" ) as string;
						info.os = string.Join( " ", new[] { product, display }.Where( s => !string.IsNullOrEmpty( s ) ) );
						var build = nt.GetValue( "CurrentBuild" ) as string;
						var ubr = nt.GetValue( "UBR" );
						info.osBuild = build is null ? null : ubr is null ? build : $"{build}.{ubr}";
					}
				}

				using( var cpu = baseKey.OpenSubKey( @"HARDWARE\DESCRIPTION\System\CentralProcessor\0" ) )
					info.cpu = ( cpu?.GetValue( "ProcessorNameString" ) as string )?.Trim();

				using( var gpus = baseKey.OpenSubKey( GpuClassKey ) )
				{
					foreach( var sub in gpus?.GetSubKeyNames().Where( n => n.Length == 4 && n.All( char.IsDigit ) ) ?? Enumerable.Empty<string>() )
					{
						try
						{
							using var k = gpus!.OpenSubKey( sub );
							var name = k?.GetValue( "DriverDesc" ) as string;
							if( string.IsNullOrEmpty( name ) ) continue;
							info.gpus.Add( new CrashGpu
							{
								name = name,
								driverVersion = k!.GetValue( "DriverVersion" ) as string,
								driverDate = k.GetValue( "DriverDate" ) as string,
							} );
						}
						catch( Exception )
						{
							// a key we may not read says nothing
						}
					}
				}
			}
			catch( Exception )
			{
			}

			try
			{
				var mem = new MemoryStatusEx();
				if( GlobalMemoryStatusEx( mem ) )
				{
					info.ramTotalMB = (long) ( mem.ullTotalPhys / 1048576 );
					info.ramFreeMB = (long) ( mem.ullAvailPhys / 1048576 );
				}
			}
			catch( Exception )
			{
			}

			try
			{
				foreach( var d in DriveInfo.GetDrives().Where( d => d.DriveType == DriveType.Fixed && d.IsReady ) )
					info.disks.Add( new CrashDisk { drive = d.Name, freeMB = d.AvailableFreeSpace / 1048576, totalMB = d.TotalSize / 1048576 } );
			}
			catch( Exception )
			{
			}

			return info;
		}
	}
}
