using System;
using System.IO;
using System.Linq;

namespace Dirigent.TestBed
{
	/// <summary>
	/// Finds the built Dirigent.TestApp.exe. The integration test project references the test
	/// app so it is always built, but its output does not land next to the tests, so it has to
	/// be located relative to the solution.
	/// </summary>
	public static class TestAppLocator
	{
		const string ExeName = "Dirigent.TestApp.exe";

		static string? _cached;

		public static string Find()
		{
			if( _cached is not null ) return _cached;

			var srcDir = FindSourceDir()
				?? throw new FileNotFoundException( "Could not find the 'src' folder above the test assembly" );

			var projectDir = Path.Combine( srcDir, "Dirigent.TestApp" );
			if( !Directory.Exists( projectDir ) )
				throw new DirectoryNotFoundException( $"Dirigent.TestApp project folder not found at {projectDir}" );

			// pick the most recently built one, whatever configuration and framework it sits under
			var candidate = Directory
				.EnumerateFiles( projectDir, ExeName, SearchOption.AllDirectories )
				.Select( p => new FileInfo( p ) )
				.OrderByDescending( f => f.LastWriteTimeUtc )
				.FirstOrDefault();

			if( candidate is null )
				throw new FileNotFoundException(
					$"{ExeName} not found under {projectDir}. Build the Dirigent.TestApp project first." );

			_cached = candidate.FullName;
			return _cached;
		}

		static string? FindSourceDir()
		{
			var dir = new DirectoryInfo( AppContext.BaseDirectory );
			while( dir is not null )
			{
				if( File.Exists( Path.Combine( dir.FullName, "Dirigent.NetCore.sln" ) ) )
					return dir.FullName;

				dir = dir.Parent;
			}
			return null;
		}
	}
}
