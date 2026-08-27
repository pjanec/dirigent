using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;

namespace Dirigent
{
	/// <summary>
	/// Scanning of the file system for the files matching the VFS file masks and limits.
	/// </summary>
	public static class FileScan
	{
		/// <summary>
		/// Finds the files under given folder matching the glob-style mask, applying the age, count and size limits.
		/// The newest files are preferred if the count/size limit applies.
		/// </summary>
		/// <param name="mask">Glob-style file mask, see <see cref="Glob"/>. Empty = all files.</param>
		/// <param name="maxAgeSeconds">Maximum age based on the last write time. 0 = whatever age.</param>
		/// <param name="maxFiles">Maximum number of files. 0 = unlimited.</param>
		/// <param name="maxTotalBytes">Maximum total size of the files. 0 = unlimited. At least one file is always returned.</param>
		/// <param name="recursive">Whether to descend into the subfolders.</param>
		/// <returns>The matching files, newest first, with their paths relative to the scanned folder.</returns>
		public static List<(string RelPath, FileInfo Info)> FindMatchingFiles(
			string folderName,
			string? mask,
			double maxAgeSeconds,
			int maxFiles,
			long maxTotalBytes,
			bool recursive = true
		)
		{
			if( string.IsNullOrEmpty( folderName ) ) folderName = Directory.GetCurrentDirectory();

			var patterns = Glob.ParseMask( mask );

			var dirInfo = new DirectoryInfo( folderName );

			var enumOpts = new EnumerationOptions()
			{
				MatchType = MatchType.Win32,
				RecurseSubdirectories = recursive,
				ReturnSpecialDirectories = false,
				IgnoreInaccessible = true, // a single unreadable subfolder must not spoil the whole scan
				AttributesToSkip = 0, // include the hidden and system files as well
			};

			// length of the prefix to strip in order to get the path relative to the scanned folder
			var rootPathLen = dirInfo.FullName.TrimEnd( System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar ).Length + 1;

			var matching = new List<(string RelPath, FileInfo Info)>();

			foreach( var info in dirInfo.EnumerateFiles( "*", enumOpts ) )
			{
				var relPath = info.FullName.Length > rootPathLen
								? info.FullName.Substring( rootPathLen )
								: info.Name;

				if( !Glob.IsMatchAny( relPath, patterns ) )
					continue;

				if( maxAgeSeconds > 0 )
				{
					var age = ( DateTime.UtcNow - info.LastWriteTimeUtc ).TotalSeconds;
					if( age > maxAgeSeconds ) continue;
				}

				matching.Add( (relPath, info) );
			}

			// newest first, so that the count/size limits keep the most interesting files
			matching.Sort( (x, y) => y.Info.LastWriteTimeUtc.CompareTo( x.Info.LastWriteTimeUtc ) );

			if( maxFiles <= 0 && maxTotalBytes <= 0 )
				return matching;

			var res = new List<(string RelPath, FileInfo Info)>();
			long totalBytes = 0;

			foreach( var item in matching )
			{
				if( maxFiles > 0 && res.Count >= maxFiles )
					break;

				if( maxTotalBytes > 0 )
				{
					// always let at least one file through, however big it is
					if( res.Count > 0 && totalBytes + item.Info.Length > maxTotalBytes )
						break;

					totalBytes += item.Info.Length;
				}

				res.Add( item );
			}

			return res;
		}
	}
}
