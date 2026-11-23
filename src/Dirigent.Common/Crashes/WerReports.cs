using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Dirigent.Crashes
{
	/// <summary>What a Windows Error Reporting <c>Report.wer</c> says about the crash it describes.</summary>
	public class WerReportInfo
	{
		public string Path = string.Empty;
		public DateTime? EventTimeUtc;
		public string? AppName;
		public string? AppPath;

		/// <summary>NsAppName of a packaged program.</summary>
		public string? NsAppName;

		/// <summary>The application name of the signature (Sig[0] when it is the "Application Name").</summary>
		public string? SigExe;
	}

	/// <summary>
	/// Finding the report of a crash by what the report itself says - its event time and the program - and not by the 1001 event, which Windows
	/// logs again, days later, for reports that were queued.
	/// </summary>
	public static class WerReports
	{
		/// <summary>A report belongs to a crash when its event time is within this of the crash.</summary>
		public static readonly TimeSpan TimeTolerance = TimeSpan.FromSeconds( 10 );

		/// <summary>Reads the key=value lines of a Report.wer (the file is UTF-16; the text is what the reader made of it).</summary>
		public static WerReportInfo Parse( string text, string path )
		{
			var info = new WerReportInfo { Path = path };
			string? sig0Name = null, sig0Value = null;

			foreach( var raw in text.Replace( "\r\n", "\n" ).Split( '\n' ) )
			{
				var line = raw.Trim( '﻿', '\r', ' ' );
				var eq = line.IndexOf( '=' );
				if( eq <= 0 ) continue;

				var key = line.Substring( 0, eq );
				var value = line.Substring( eq + 1 ).Trim();

				switch( key )
				{
					case "EventTime":
						if( long.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out var ft ) && ft > 0 )
						{
							try { info.EventTimeUtc = DateTime.FromFileTimeUtc( ft ); }
							catch( ArgumentOutOfRangeException ) { }
						}
						break;
					case "NsAppName": info.NsAppName ??= value; break;
					case "AppName": info.AppName ??= value; break;
					case "AppPath": info.AppPath ??= value; break;
					case "Sig[0].Name": sig0Name = value; break;
					case "Sig[0].Value": sig0Value = value; break;
				}
			}

			if( sig0Name is not null && sig0Name.Contains( "Application", StringComparison.OrdinalIgnoreCase ) ) info.SigExe = sig0Value;
			return info;
		}

		static string? NameOf( string? path )
		{
			var p = CrashAppTable.NormalizePath( path );
			return p is null ? null : p.Substring( p.LastIndexOfAny( new[] { (char) 92, '/' } ) + 1 );
		}

		/// <summary>The report of the crash among the reports: the program is the one (by path, else by name) and the event time is within a few seconds.</summary>
		public static WerReportInfo? Match( IEnumerable<WerReportInfo> reports, string? exeName, string? exePath, DateTime crashTimeUtc )
		{
			var path = CrashAppTable.NormalizePath( exePath );

			bool SameProgram( WerReportInfo r )
			{
				if( path is not null && r.AppPath is not null )
					return string.Equals( CrashAppTable.NormalizePath( r.AppPath ), path, StringComparison.OrdinalIgnoreCase );

				var name = exeName ?? NameOf( path );
				if( name is null ) return false;
				return new[] { NameOf( r.AppPath ), r.NsAppName, r.AppName, r.SigExe }.Any( n => n is not null && string.Equals( n, name, StringComparison.OrdinalIgnoreCase ) );
			}

			return reports.Where( r => r.EventTimeUtc is DateTime t && ( t - crashTimeUtc ).Duration() <= TimeTolerance && SameProgram( r ) )
							.OrderBy( r => ( r.EventTimeUtc!.Value - crashTimeUtc ).Duration() ).FirstOrDefault();
		}

		/// <summary>The folders Windows keeps reports in: the machine's queue and archive, and those of the user the agent runs as.</summary>
		public static IEnumerable<string> ReportRoots()
		{
			var common = Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData );
			var local = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );

			foreach( var baseDir in new[] { common, local } )
			{
				if( string.IsNullOrEmpty( baseDir ) ) continue;
				foreach( var sub in new[] { "ReportQueue", "ReportArchive" } )
					yield return Path.Combine( baseDir, "Microsoft", "Windows", "WER", sub );
			}
		}

		/// <summary>The reports read so far by one lookup session (an agent query), by file: reading a Report.wer is the cost.</summary>
		public class Cache
		{
			public readonly Dictionary<string, WerReportInfo?> Reports = new( StringComparer.OrdinalIgnoreCase );
		}

		/// <summary>
		/// Looks in the report folders for the report of the crash. Windows truncates the names of the folders ("AppCrash_ConsoleAp_...",
		/// "AppCrash_ConsoleApp.e_..."), so they are not matched by the program: the AppCrash_ and AppHang_ folders written within a day of the crash
		/// are read, and each Report.wer says by itself what it is.
		/// </summary>
		public static string? Find( string? exeName, string? exePath, DateTime crashTimeUtc, IEnumerable<string>? roots = null, Cache? cache = null )
		{
			cache ??= new Cache();
			var reports = new List<WerReportInfo>();

			foreach( var root in roots ?? ReportRoots() )
			{
				try
				{
					if( !Directory.Exists( root ) ) continue;

					foreach( var dirInfo in new DirectoryInfo( root ).EnumerateDirectories() )
					{
						if( !dirInfo.Name.StartsWith( "AppCrash_", StringComparison.OrdinalIgnoreCase ) && !dirInfo.Name.StartsWith( "AppHang_", StringComparison.OrdinalIgnoreCase ) ) continue;
						if( ( dirInfo.LastWriteTimeUtc - crashTimeUtc ).Duration() > TimeSpan.FromDays( 1 ) ) continue;

						var file = System.IO.Path.Combine( dirInfo.FullName, "Report.wer" );
						if( !cache.Reports.TryGetValue( file, out var info ) )
						{
							try { info = File.Exists( file ) ? Parse( File.ReadAllText( file ), file ) : null; }
							catch( Exception ) { info = null; } // a report we cannot read is none
							cache.Reports[file] = info;
						}
						if( info is not null ) reports.Add( info );
					}
				}
				catch( Exception )
				{
					// a folder we may not list has none for us
				}
			}

			return Match( reports, exeName, exePath, crashTimeUtc )?.Path;
		}
	}
}
