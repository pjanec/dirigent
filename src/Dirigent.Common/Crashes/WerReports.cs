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

		/// <summary>The process id, from the start of AppSessionGuid (eight hex digits); null when the report has none.</summary>
		public int? Pid;

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
					case "AppSessionGuid":
						if( value.Length >= 8 && int.TryParse( value.Substring( 0, 8 ), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pid ) && pid > 0 ) info.Pid ??= pid;
						break;
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

		/// <summary>A crash to find the report of.</summary>
		public class CrashToMatch
		{
			public string Key = string.Empty;
			public string? ExeName;
			public string? ExePath;
			public DateTime TimeUtc;
			public int? Pid;
		}

		static bool SameProgram( WerReportInfo r, string? exeName, string? exePath )
		{
			var path = CrashAppTable.NormalizePath( exePath );

			// both know a path: it decides
			if( path is not null && r.AppPath is not null )
				return string.Equals( CrashAppTable.NormalizePath( r.AppPath ), path, StringComparison.OrdinalIgnoreCase );

			var name = exeName ?? NameOf( path );
			if( name is null ) return false;
			return new[] { NameOf( r.AppPath ), r.NsAppName, r.AppName, r.SigExe }.Any( n => n is not null && string.Equals( n, name, StringComparison.OrdinalIgnoreCase ) );
		}

		/// <summary>
		/// One report for each crash, and no report for two: every pair of a crash and a report of its program within a few seconds is a candidate, a pair whose
		/// process ids agree comes first, then the nearest in time - and a report taken by a nearer crash is not given again. A pair whose ids are both known and differ is
		/// no candidate.
		/// </summary>
		public static Dictionary<string, WerReportInfo> Assign( IEnumerable<CrashToMatch> crashes, IEnumerable<WerReportInfo> reports )
		{
			var reportList = reports.Where( r => r.EventTimeUtc is not null ).ToList();

			var pairs = new List<(CrashToMatch crash, WerReportInfo report, int pidRank, TimeSpan diff)>();
			foreach( var c in crashes )
				foreach( var r in reportList )
				{
					var diff = ( r.EventTimeUtc!.Value - c.TimeUtc ).Duration();
					if( diff > TimeTolerance || !SameProgram( r, c.ExeName, c.ExePath ) ) continue;
					if( c.Pid is int cp && r.Pid is int rp && cp != rp ) continue;

					pairs.Add( (c, r, c.Pid is not null && c.Pid == r.Pid ? 0 : 1, diff) );
				}

			var result = new Dictionary<string, WerReportInfo>();
			var taken = new HashSet<WerReportInfo>();
			foreach( var p in pairs.OrderBy( p => p.pidRank ).ThenBy( p => p.diff ) )
			{
				if( result.ContainsKey( p.crash.Key ) || taken.Contains( p.report ) ) continue;
				result[p.crash.Key] = p.report;
				taken.Add( p.report );
			}
			return result;
		}

		/// <summary>The report of one crash among the reports.</summary>
		public static WerReportInfo? Match( IEnumerable<WerReportInfo> reports, string? exeName, string? exePath, DateTime crashTimeUtc, int? pid = null )
			=> Assign( new[] { new CrashToMatch { Key = "x", ExeName = exeName, ExePath = exePath, TimeUtc = crashTimeUtc, Pid = pid } }, reports ).GetValueOrDefault( "x" );

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
		/// The reports of the AppCrash_ and AppHang_ folders written within a day of the times. Windows truncates the names of the folders
		/// ("AppCrash_ConsoleAp_..."), so they are not matched by program here: each Report.wer says by itself what it is.
		/// </summary>
		public static List<WerReportInfo> List( DateTime fromUtc, DateTime toUtc, IEnumerable<string>? roots = null, Cache? cache = null )
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
						var file = System.IO.Path.Combine( dirInfo.FullName, "Report.wer" );

						// Windows re-processes queued reports for days and may touch their folders, so the write time alone says nothing:
						// a folder is read when its creation time, its write time or the creation time of its report is near the window
						bool Near( DateTime t ) => t >= fromUtc.AddDays( -1 ) && t <= toUtc.AddDays( 1 );
						DateTime? fileCreated = null;
						try { if( File.Exists( file ) ) fileCreated = File.GetCreationTimeUtc( file ); } catch( Exception ) { }
						if( !Near( dirInfo.CreationTimeUtc ) && !Near( dirInfo.LastWriteTimeUtc ) && !( fileCreated is DateTime fc && Near( fc ) ) ) continue;

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

			return reports;
		}

		/// <summary>The report of one crash, read from the report folders.</summary>
		public static string? Find( string? exeName, string? exePath, DateTime crashTimeUtc, IEnumerable<string>? roots = null, Cache? cache = null, int? pid = null )
			=> Match( List( crashTimeUtc, crashTimeUtc, roots, cache ), exeName, exePath, crashTimeUtc, pid )?.Path;
	}
}
