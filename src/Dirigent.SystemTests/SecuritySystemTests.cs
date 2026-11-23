using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System.Diagnostics;
using System.Net.Sockets;

namespace Dirigent.SystemTests
{
	/// <summary>
	/// Security on, with real processes: the executables find the site secret where they are told it lies, connect over TLS, keep out one that
	/// holds another secret, and do not start at all when the secret is missing.
	/// </summary>
	[TestClass()]
	public class SecuritySystemTests
	{
		static string LogText( SystemWorld world, string machine )
		{
			var path = world.Files.LogOf( machine );
			if( !File.Exists( path ) ) return "";
			using var file = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete );
			using var reader = new StreamReader( file );
			return reader.ReadToEnd();
		}

		[TestMethod()]
		public void ASecuredSiteConnectsOverTlsAndKeepsOutAnAgentWithAnotherSecret()
		{
			// master and agent, secret in site.secret beside the LocalConfig.xml, the CLI not named
			using var world = SystemWorld.Start( secured: true, cliNamed: false );

			var master = world.Files.MasterMachine;
			var masterLog = LogText( world, master );
			StringAssert.Contains( masterLog, "The master port is secured" );

			// a third agent with the right secret, found the same way, gets in: the control
			world.StartExtraAgent( "extra" );
			world.WaitUntil( "an agent with the site secret connects", TimeSpan.FromSeconds( 40 ), () => world.ConnectedClientsOverHttp().Contains( "extra" ) );

			// and one with another secret does not
			var otherFile = Path.Combine( world.Root, "other.secret" );
			File.WriteAllText( otherFile, Guid.NewGuid().ToString( "N" ) + Guid.NewGuid().ToString( "N" ) );
			world.StartExtraAgent( "intruder", otherFile );

			world.WaitUntil( "the master logs the wrong secret", TimeSpan.FromSeconds( 40 ), () => LogText( world, master ).Contains( "wrong site secret (or not the same Dirigent)" ) );
			world.WaitUntil( "the intruder logs that the master did not prove the secret", TimeSpan.FromSeconds( 20 ),
				() => LogText( world, "intruder" ).Contains( "did not prove the site secret - wrong secret, or someone in between" ) );

			var connected = world.ConnectedClientsOverHttp();
			Assert.IsFalse( connected.Contains( "intruder" ), "the agent with the wrong secret stays out: /api/clients" );
			Assert.IsTrue( connected.Contains( "extra" ) && connected.IsSupersetOf( world.Files.Machines ), "everybody else is still there" );
		}

		[TestMethod()]
		public void TheTelnetCliIsOffWithSecurityOnUnlessAPortIsNamed()
		{
			using var world = SystemWorld.Start( secured: true, cliNamed: false );

			var masterLog = LogText( world, world.Files.MasterMachine );
			StringAssert.Contains( masterLog, "The Command Line Interface is off: security is on and no CLIPort is named." );

			using var tcp = new TcpClient();
			var refused = false;
			try { refused = !( tcp.ConnectAsync( System.Net.IPAddress.Loopback, world.CliPort ).Wait( TimeSpan.FromSeconds( 3 ) ) && tcp.Connected ); }
			catch( AggregateException e ) when( e.InnerException is SocketException ) { refused = true; }
			Assert.IsTrue( refused, "nobody listens on the CLI port" );
		}

		[TestMethod()]
		public void ANamedCliPortKeepsTheCliWithSecurityOnAndSaysItIsNotChecked()
		{
			using var world = SystemWorld.Start( secured: true, cliNamed: true );

			// the world was driven over the CLI to get here: it runs
			Assert.IsTrue( world.CliList( "GetAllClientsState" ).Count >= world.Files.Machines.Count );

			StringAssert.Contains( LogText( world, world.Files.MasterMachine ), "although security is on: it runs every command for anybody who can reach that port" );
		}

		[TestMethod()]
		public void AProcessWithSecurityOnAndNoSecretDoesNotStartAndSaysWhereItLooked()
		{
			var folder = Path.Combine( Path.GetTempPath(), "dirigent-nosecret-" + Guid.NewGuid().ToString( "N" ).Substring( 0, 6 ) );
			Directory.CreateDirectory( folder );
			try
			{
				var exe = SystemWorld.Tool( "Dirigent.Agent.Console", "Dirigent.Agent.exe" );
				var local = Path.Combine( folder, "LocalConfig.xml" );

				var run = SystemWorld.RunTool( exe, new[] { "--security", "on", "--mode", "daemon", "--machineId", "nosecret", "--localConfigFile", local, "--logFile", Path.Combine( folder, "x.log" ) } );

				Assert.AreEqual( 5, run.ExitCode, $"the exit code of a security configuration error: {run.All}" );
				StringAssert.Contains( run.All, "site.secret" );
				StringAssert.Contains( run.All, Path.Combine( folder, "site.secret" ), "the folder of the LocalConfig.xml is named" );
				StringAssert.Contains( run.All, Path.GetDirectoryName( exe )!, "and the folder of the executable" );

				// a secret that is too short, too
				File.WriteAllText( Path.Combine( folder, "site.secret" ), "short" );
				var run2 = SystemWorld.RunTool( exe, new[] { "--security", "on", "--mode", "daemon", "--machineId", "nosecret", "--localConfigFile", local, "--logFile", Path.Combine( folder, "x.log" ) } );
				Assert.AreEqual( 5, run2.ExitCode, run2.All );
				StringAssert.Contains( run2.All, "5 characters" );
			}
			finally
			{
				try { Directory.Delete( folder, true ); } catch { }
			}
		}
	}
}
