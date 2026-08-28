using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dirigent.Tests
{
	/// <summary>
	/// Turning a machine-local path into a UNC path through the machine's file shares.
	/// </summary>
	[TestClass()]
	public class MakeUncTests
	{
		const string _machineId = "m1";
		const string _ip = "192.168.1.100";

		class DirigStub : IDirig
		{
			public string Name => "operator";

			public Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName,
					string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
				=> throw new NotImplementedException();

			public Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent )
				=> throw new NotImplementedException();
		}

		/// <param name="shares">share name and folder, in the order they are declared in the config</param>
		static FileRegistry RegistryWithShares( params (string Name, string Path)[] shares )
		{
			var shareDefs = new List<FileShareDef>();
			foreach( var (name, path) in shares )
				shareDefs.Add( new FileShareDef() { MachineId = _machineId, Name = name, Path = path } );

			var reg = new FileRegistry( new DirigStub(), "operator", @"C:\", ( machineId ) => _ip );
			reg.SetMachines( new List<MachineDef>()
			{
				new MachineDef() { Id = _machineId, IP = _ip, FileShares = shareDefs }
			} );
			return reg;
		}

		[TestMethod()]
		public void WholeDriveShareTest()
		{
			var reg = RegistryWithShares( ("C", @"C:\") );

			Assert.AreEqual( $@"\\{_ip}\C\Logs\app.log",
				reg.MakeUNC( @"C:\Logs\app.log", _machineId, "test" ) );
		}

		[TestMethod()]
		public void MostSpecificShareWinsTest()
		{
			// the share made for the log folder is to be preferred over the one covering the
			// whole drive - it is the one whose permissions were set up for this
			var reg = RegistryWithShares( ("C", @"C:\"), ("Logs", @"C:\Logs") );

			Assert.AreEqual( $@"\\{_ip}\Logs\app.log",
				reg.MakeUNC( @"C:\Logs\app.log", _machineId, "test" ) );
		}

		[TestMethod()]
		public void ShareOrderDoesNotMatterTest()
		{
			// the same shares declared the other way round must give the same answer
			var reg = RegistryWithShares( ("Logs", @"C:\Logs"), ("C", @"C:\") );

			Assert.AreEqual( $@"\\{_ip}\Logs\app.log",
				reg.MakeUNC( @"C:\Logs\app.log", _machineId, "test" ) );
		}

		[TestMethod()]
		public void DeeperShareWinsOverShallowOneTest()
		{
			var reg = RegistryWithShares( ("C", @"C:\"), ("Logs", @"C:\Logs"), ("Today", @"C:\Logs\today") );

			Assert.AreEqual( $@"\\{_ip}\Today\app.log",
				reg.MakeUNC( @"C:\Logs\today\app.log", _machineId, "test" ) );

			// ...but only for the paths it really covers
			Assert.AreEqual( $@"\\{_ip}\Logs\yesterday\app.log",
				reg.MakeUNC( @"C:\Logs\yesterday\app.log", _machineId, "test" ) );
		}

		[TestMethod()]
		public void ShareMatchesAtFolderBoundaryOnlyTest()
		{
			// "C:\Logs" is not a prefix of "C:\LogsBackup" in any sense that matters
			var reg = RegistryWithShares( ("C", @"C:\"), ("Logs", @"C:\Logs") );

			Assert.AreEqual( $@"\\{_ip}\C\LogsBackup\app.log",
				reg.MakeUNC( @"C:\LogsBackup\app.log", _machineId, "test" ) );
		}

		[TestMethod()]
		public void TrailingSeparatorOfShareIgnoredTest()
		{
			// the share folder may or may not be written with a trailing separator
			var reg = RegistryWithShares( ("Logs", @"C:\Logs\") );

			Assert.AreEqual( $@"\\{_ip}\Logs\app.log",
				reg.MakeUNC( @"C:\Logs\app.log", _machineId, "test" ) );
		}

		[TestMethod()]
		public void ShareRootItselfTest()
		{
			// a <Folder> node pointing at the share's own folder resolves to the share itself
			var reg = RegistryWithShares( ("Logs", @"C:\Logs") );

			Assert.AreEqual( $@"\\{_ip}\Logs",
				reg.MakeUNC( @"C:\Logs", _machineId, "test" ) );
		}

		[TestMethod()]
		public void CaseInsensitiveTest()
		{
			var reg = RegistryWithShares( ("Logs", @"c:\logs") );

			Assert.AreEqual( $@"\\{_ip}\Logs\app.log",
				reg.MakeUNC( @"C:\Logs\app.log", _machineId, "test" ) );
		}

		[TestMethod()]
		public void NoCoveringShareTest()
		{
			var reg = RegistryWithShares( ("C", @"C:\") );

			var ex = Assert.ThrowsException<Exception>(
				() => reg.MakeUNC( @"D:\Logs\app.log", _machineId, "the log file" ) );

			StringAssert.Contains( ex.Message, "No file share matching" );
			StringAssert.Contains( ex.Message, "the log file", "the message says what was being resolved" );
		}

		[TestMethod()]
		public void GlobalPathStaysAsItIsTest()
		{
			var reg = RegistryWithShares( ("C", @"C:\") );

			// a node with no machine is expected to carry a UNC path already
			Assert.AreEqual( @"\\server\share\cfg.xml",
				reg.MakeUNC( @"\\server\share\cfg.xml", null, "test" ) );
		}
	}
}
