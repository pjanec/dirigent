using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;

namespace Dirigent
{
	/// <summary>
	/// Glob-style file mask matching, used by the VFS file masks (see docs/Files.md).
	/// </summary>
	/// <remarks>
	/// Supported syntax:
	///   *      any number of characters within a single path segment
	///   ?      a single character
	///   **     any number of path segments (must form a whole segment, like "logs/**\/*.txt")
	///   {a,b}  alternatives; expanded into separate patterns before matching. Can be nested.
	/// Matching is case insensitive, as the paths are expected to be Windows ones.
	/// </remarks>
	public static class Glob
	{
		static readonly char[] _seps = new char[] { '/', '\\' };

		public static bool ContainsPathSeparator( string pattern ) => pattern.IndexOfAny( _seps ) >= 0;

		/// <summary>
		/// Expands the {a,b} alternatives, returning one plain pattern per combination.
		/// A pattern with unbalanced braces is returned as it is (treated as literal).
		/// </summary>
		public static List<string> ExpandAlternatives( string pattern )
		{
			var res = new List<string>();

			int open = pattern.IndexOf( '{' );
			if( open < 0 )
			{
				res.Add( pattern );
				return res;
			}

			// find the brace closing the first opening one
			int close = -1;
			int depth = 0;
			for( int i = open; i < pattern.Length; i++ )
			{
				if( pattern[i] == '{' ) depth++;
				else
				if( pattern[i] == '}' )
				{
					depth--;
					if( depth == 0 ) { close = i; break; }
				}
			}

			if( close < 0 ) // unbalanced
			{
				res.Add( pattern );
				return res;
			}

			var prefix = pattern.Substring( 0, open );
			var suffix = pattern.Substring( close + 1 );
			var inner = pattern.Substring( open + 1, close - open - 1 );

			// split the alternatives by the commas belonging to this brace level
			var alternatives = new List<string>();
			depth = 0;
			int start = 0;
			for( int i = 0; i < inner.Length; i++ )
			{
				if( inner[i] == '{' ) depth++;
				else
				if( inner[i] == '}' ) depth--;
				else
				if( inner[i] == ',' && depth == 0 )
				{
					alternatives.Add( inner.Substring( start, i - start ) );
					start = i + 1;
				}
			}
			alternatives.Add( inner.Substring( start ) );

			// the remaining braces (if any) are expanded by the recursive calls
			foreach( var alt in alternatives )
			{
				res.AddRange( ExpandAlternatives( prefix + alt + suffix ) );
			}

			return res;
		}

		/// <summary>
		/// Matches a path relative to some root folder against a single pattern (containing no alternatives).
		/// </summary>
		public static bool IsMatch( string relativePath, string pattern )
		{
			var path = relativePath.Split( _seps, StringSplitOptions.RemoveEmptyEntries );
			var pat = pattern.Split( _seps, StringSplitOptions.RemoveEmptyEntries );
			return MatchSegments( path, 0, pat, 0 );
		}

		public static bool IsMatchAny( string relativePath, IEnumerable<string> patterns )
		{
			foreach( var p in patterns )
			{
				if( IsMatch( relativePath, p ) ) return true;
			}
			return false;
		}

		static bool MatchSegments( string[] path, int pathIdx, string[] pat, int patIdx )
		{
			while( patIdx < pat.Length )
			{
				if( pat[patIdx] == "**" )
				{
					// a trailing '**' matches whatever is left, including nothing
					if( patIdx == pat.Length - 1 )
						return true;

					// try to match the rest of the pattern after skipping any number of segments
					for( int skip = pathIdx; skip <= path.Length; skip++ )
					{
						if( MatchSegments( path, skip, pat, patIdx + 1 ) )
							return true;
					}
					return false;
				}

				if( pathIdx >= path.Length )
					return false;

				if( !FileSystemName.MatchesSimpleExpression( pat[patIdx], path[pathIdx] ) )
					return false;

				pathIdx++;
				patIdx++;
			}

			// the whole pattern is consumed - it is a match only if the whole path is consumed as well
			return pathIdx == path.Length;
		}

		/// <summary>
		/// Turns a user-supplied file mask into the list of patterns to match the paths relative
		/// to the scanned folder against.
		/// </summary>
		/// <remarks>
		/// An empty mask matches all files at any depth.
		/// A mask containing no path separator is matched against the file name at any depth, so that
		/// masks like "*.log" keep working the way they did before the glob support was added.
		/// </remarks>
		public static List<string> ParseMask( string? mask )
		{
			if( string.IsNullOrEmpty( mask ) ) mask = "*";

			var res = new List<string>();
			foreach( var expanded in ExpandAlternatives( mask ) )
			{
				var pattern = expanded;

				// "*.*" means "anything" by the Win32 convention, while as a glob it would require a dot
				if( pattern == "*.*" ) pattern = "*";

				// a plain file name mask applies at any depth
				if( pattern != "**" && !ContainsPathSeparator( pattern ) )
					pattern = "**/" + pattern;

				res.Add( pattern );
			}
			return res;
		}
	}
}
