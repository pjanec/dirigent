using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace Dirigent
{
	public static class PathUtils
	{
		public static bool IsPathAbsolute( string path )
		{
			return System.IO.Path.IsPathFullyQualified(path) && System.IO.Path.IsPathRooted(path);
		}

		public static string BuildAbsolutePath( string anyPath, string relativePathsRoot )
		{
			if( Path.IsPathRooted( anyPath ) )
				return anyPath;

			return Path.Combine( relativePathsRoot, anyPath );
		}

		/// <summary>
		/// combines 
		/// </summary>
		/// <param name="sharedConfigFileNameFromConfig"></param>
		/// <param name="rootFromConfig"></param>
		/// <returns></returns>
		public static string GetRootForRelativePaths( string? sharedConfigFileNameFromConfig, string? rootFromConfig )
		{
			if( !string.IsNullOrEmpty( rootFromConfig ) )
			{
				return rootFromConfig;
			}

			//_rootForRelativePaths = System.IO.Directory.GetCurrentDirectory();
			if( !string.IsNullOrEmpty(sharedConfigFileNameFromConfig) )
			{
				return System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath( sharedConfigFileNameFromConfig ) ) ?? System.IO.Directory.GetCurrentDirectory();
			}

			return System.IO.Directory.GetCurrentDirectory();
		}

        public static string GetRelativePath(string filespec, string folder)
        {
            //https://stackoverflow.com/questions/703281/getting-path-relative-to-the-current-working-directory
            Uri pathUri = new Uri(Path.GetFullPath(filespec));
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(Path.GetFullPath(folder));
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
	}

}
