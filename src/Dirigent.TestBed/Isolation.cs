using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Dirigent.TestBed
{
	/// <summary>
	/// Everything a test run has to keep to itself so that two runs, or a run and a real
	/// Dirigent installation, cannot interfere with each other.
	/// </summary>
	public static class Isolation
	{
		/// <summary>
		/// Short tag identifying one test run, used to keep machine ids and folders apart.
		/// </summary>
		public static string NewRunTag()
			=> Guid.NewGuid().ToString( "N" ).Substring( 0, 6 );

		/// <summary>
		/// A TCP port nobody is listening on. There is an unavoidable race between finding the
		/// port and the server binding it, but the window is small and the alternative - a fixed
		/// port - fails far more often when tests run in parallel or a real agent is running.
		/// </summary>
		public static int FreeTcpPort()
		{
			var listener = new TcpListener( IPAddress.Loopback, 0 );
			listener.Start();
			try
			{
				return ( (IPEndPoint) listener.LocalEndpoint ).Port;
			}
			finally
			{
				listener.Stop();
			}
		}

		/// <summary>
		/// Per-run scratch folder: configs, application working directories, seeded files,
		/// and the redirected download folder all live under here.
		/// </summary>
		public static string CreateTempRoot( string runTag )
		{
			var path = Path.Combine( Path.GetTempPath(), "DirigentTestBed", runTag );
			Directory.CreateDirectory( path );
			return path;
		}

		public static void DeleteTempRoot( string path )
		{
			for( int attempt = 0; attempt < 3; attempt++ )
			{
				try
				{
					if( Directory.Exists( path ) ) Directory.Delete( path, true );
					return;
				}
				catch( IOException )
				{
					System.Threading.Thread.Sleep( 100 ); // a killed process may still hold a handle
				}
				catch( UnauthorizedAccessException )
				{
					System.Threading.Thread.Sleep( 100 );
				}
			}
		}

	}
}
