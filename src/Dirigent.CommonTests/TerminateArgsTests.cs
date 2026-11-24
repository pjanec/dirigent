using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Dirigent.Tests
{
	/// <summary>"Terminate killApps=0" must leave the apps running; it used to be ignored and they were killed whatever was asked.</summary>
	[TestClass()]
	public class TerminateArgsTests
	{
		static (TerminateArgs Args, string MachineId) Parse( params string[] args ) => Commands.Terminate.ParseArgs( new List<string>( args ) );

		[TestMethod()]
		public void KillAppsFollowsWhatWasAskedTest()
		{
			Assert.IsFalse( Parse( "killApps=0" ).Args.KillApps, "killApps=0 keeps the apps" );
			Assert.IsTrue( Parse( "killApps=1" ).Args.KillApps );
			Assert.IsTrue( Parse().Args.KillApps, "left out: killed, as ever" );
			Assert.IsFalse( Parse( "KILLAPPS=0", "machineId=m2" ).Args.KillApps );
			Assert.AreEqual( "m2", Parse( "killApps=0", "machineId=m2" ).MachineId );
		}
	}
}
