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

	}

}
