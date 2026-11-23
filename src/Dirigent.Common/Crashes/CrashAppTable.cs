using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dirigent.Crashes
{
	/// <summary>One program of the site, as its machine launches it.</summary>
	public class CrashApp
	{
		/// <summary>"m1.a"</summary>
		public string App = string.Empty;
		public string? Component;

		/// <summary>The program, expanded and absolute; null when the definition gives only a bare name.</summary>
		public string? ExePath;

		public string ExeName = string.Empty;
		public string? StartDir;
		public string? CmdLine;
	}

	/// <summary>Who an exe is.</summary>
	public class CrashOwner
	{
		public List<CrashApp> Candidates = new();

		/// <summary>One of Dirigent's own programs (no app).</summary>
		public bool IsOwn;

		public bool IsOurs => IsOwn || Candidates.Count > 0;

		/// <summary>The app when exactly one candidate; never a guess.</summary>
		public string? App => Candidates.Count == 1 ? Candidates[0].App : null;

		public List<string> Apps => Candidates.Select( c => c.App ).Distinct( StringComparer.OrdinalIgnoreCase ).ToList();

		/// <summary>The one component of the candidates; null when there are none or they differ. "Dirigent" for Dirigent's own.</summary>
		public string? Component
		{
			get
			{
				if( Candidates.Count == 0 ) return IsOwn ? CrashAppTable.OwnComponent : null;
				var first = Candidates[0].Component;
				return Candidates.All( c => string.Equals( c.Component, first, StringComparison.OrdinalIgnoreCase ) ) ? first : null;
			}
		}
	}

	/// <summary>The programs of the site on one machine: exe to app to component. Pure.</summary>
	public class CrashAppTable
	{
		public const string OwnComponent = "Dirigent";

		public List<CrashApp> Apps { get; } = new();

		/// <summary>Dirigent's own programs: full paths or names.</summary>
		public List<string> OwnExes { get; } = new();

		public static string? NormalizePath( string? path )
		{
			if( string.IsNullOrWhiteSpace( path ) ) return null;
			path = path.Trim().Trim( '\0', '"' );
			if( path.StartsWith( @"\\?\" ) ) path = path.Substring( 4 );
			return path.Replace( '/', '\\' ).TrimEnd( '\\' );
		}

		static string NameOf( string path ) => path.Substring( path.LastIndexOfAny( new[] { '\\', '/' } ) + 1 );

		static bool IsRooted( string path ) => path.Length > 2 && ( path[1] == ':' || path.StartsWith( @"\\" ) );

		static bool Same( string? a, string? b )
			=> NormalizePath( a ) is string x && NormalizePath( b ) is string y && string.Equals( x, y, StringComparison.OrdinalIgnoreCase );

		/// <summary>
		/// Finds the apps a crashed exe may be. By its full path when the event has one, by its file name when it has only that;
		/// among several, by the start folder and the command line when the event has them.
		/// </summary>
		public CrashOwner Find( string? exePath, string? exeName = null, string? startDir = null, string? cmdLine = null )
		{
			var path = NormalizePath( exePath );
			var name = string.IsNullOrWhiteSpace( exeName ) ? ( path is null ? null : NameOf( path ) ) : exeName.Trim();
			var owner = new CrashOwner();
			if( name is null && path is null ) return owner;

			if( path is not null && IsRooted( path ) )
			{
				// an app that gives no full path (a bare name, found on the PATH) can be told only by its name
				owner.Candidates = Apps.Where( a => a.ExePath is null
							? string.Equals( a.ExeName, name, StringComparison.OrdinalIgnoreCase )
							: Same( a.ExePath, path ) ).ToList();
			}
			else
			{
				owner.Candidates = Apps.Where( a => string.Equals( a.ExeName, name, StringComparison.OrdinalIgnoreCase ) ).ToList();
			}

			if( owner.Candidates.Count > 1 && !string.IsNullOrEmpty( startDir ) )
			{
				var narrowed = owner.Candidates.Where( a => Same( a.StartDir, startDir ) ).ToList();
				if( narrowed.Count > 0 ) owner.Candidates = narrowed;
			}

			if( owner.Candidates.Count > 1 && !string.IsNullOrEmpty( cmdLine ) )
			{
				var narrowed = owner.Candidates.Where( a => string.Equals( ( a.CmdLine ?? "" ).Trim(), cmdLine.Trim(), StringComparison.OrdinalIgnoreCase ) ).ToList();
				if( narrowed.Count > 0 ) owner.Candidates = narrowed;
			}

			if( owner.Candidates.Count == 0 )
			{
				owner.IsOwn = OwnExes.Any( o => IsRooted( NormalizePath( o ) ?? "" )
						? ( path is not null && IsRooted( path ) ? Same( o, path ) : string.Equals( NameOf( NormalizePath( o )! ), name, StringComparison.OrdinalIgnoreCase ) )
						: string.Equals( NormalizePath( o ), name, StringComparison.OrdinalIgnoreCase ) );
			}

			return owner;
		}
	}
}
