using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dirigent.Tests
{
	/// <summary>
	/// The table of text commands: what exists, and how each one's answer ends.
	/// </summary>
	/// <remarks>
	/// A golden list rather than a rule, deliberately. Every entry here is something a client's read
	/// loop depends on - a batch file, a telnet session, a plan step - so changing one has to be a
	/// deliberate edit of this test rather than a side effect of touching a command.
	/// </remarks>
	[TestClass()]
	public class CliCommandTableTests
	{
		/// <summary>
		/// Every command, and the line that ends its answer. `Ack` unless stated.
		/// </summary>
		static readonly Dictionary<string, ETerminator> _expected = new()
		{
			{ "StartPlan",          ETerminator.Ack },
			{ "StopPlan",           ETerminator.Ack },
			{ "KillPlan",           ETerminator.Ack },
			{ "RestartPlan",        ETerminator.Ack },
			{ "LaunchApp",          ETerminator.Ack },
			{ "StartApp",           ETerminator.Ack },
			{ "KillApp",            ETerminator.Ack },
			{ "RestartApp",         ETerminator.Ack },
			{ "SelectPlan",         ETerminator.Ack },
			{ "GetPlanState",       ETerminator.SingleLine },
			{ "GetAppState",        ETerminator.SingleLine },
			{ "GetAllPlansState",   ETerminator.End },
			{ "GetAllAppsState",    ETerminator.End },
			{ "SetVars",            ETerminator.Ack },
			{ "KillAll",            ETerminator.Ack },
			{ "Shutdown",           ETerminator.Ack },
			{ "WakeUp",             ETerminator.Ack },
			{ "Terminate",          ETerminator.Ack },
			{ "Reinstall",          ETerminator.Ack },
			{ "ReloadSharedConfig", ETerminator.Ack },
			{ "StartScript",        ETerminator.Ack },
			{ "KillScript",         ETerminator.Ack },
			{ "GetScriptState",     ETerminator.SingleLine },
			{ "WaitForScript",      ETerminator.End },
			{ "GetAppFiles",        ETerminator.End },
			{ "GetAppLocation", ETerminator.End },
			{ "GetAppLocations", ETerminator.End },
			{ "MarkFiles", ETerminator.End },
			{ "ClearFiles", ETerminator.End },
			{ "UnmarkFiles", ETerminator.End },
			{ "MarkAppFiles", ETerminator.End },
			{ "ClearAppFiles", ETerminator.End },
			{ "UnmarkAppFiles", ETerminator.End },
			{ "GetFiles",       ETerminator.End },
			{ "GetFileDefs", ETerminator.SingleLine },
			{ "DownloadPackage", ETerminator.End },
			{ "DownloadApp", ETerminator.End },
			{ "GetDownload", ETerminator.SingleLine },
			{ "DeleteDownload", ETerminator.Ack },
			{ "ApplyPlan",          ETerminator.Ack },
			{ "GetClientState",     ETerminator.SingleLine },
			{ "GetAllClientsState", ETerminator.End },
		};

		[TestMethod()]
		public void TheCommandSetIsWhatTheGoldenListSaysTest()
		{
			var registered = DirigentCommandRegistrator.CommandNames.ToList();

			CollectionAssert.AreEquivalent( _expected.Keys.ToList(), registered,
				"a command was added or removed; update this list and check docs/CLI.md with it" );
		}

		[TestMethod()]
		public void NoCommandNameIsRegisteredTwiceTest()
		{
			var duplicates = DirigentCommandRegistrator.CommandNames
					.GroupBy( n => n )
					.Where( g => g.Count() > 1 )
					.Select( g => g.Key )
					.ToList();

			Assert.AreEqual( 0, duplicates.Count,
				$"the later registration would silently win: {string.Join( ", ", duplicates )}" );
		}

		[TestMethod()]
		public void EveryCommandEndsItsAnswerAsTheGoldenListSaysTest()
		{
			foreach( var (name, terminator) in _expected )
			{
				Assert.AreEqual( terminator, DirigentCommandRegistrator.TerminatorOf( name ),
					$"'{name}' answers differently than every client expects" );
			}
		}

		[TestMethod()]
		public void AnUnknownCommandIsReportedAsAckTerminatedTest()
		{
			// which is what a client waiting for one would do anyway: the master answers such a
			// request with a single ERROR, and ERROR ends every wait
			Assert.AreEqual( ETerminator.Ack, DirigentCommandRegistrator.TerminatorOf( "NoSuchCommand" ) );
		}

		[TestMethod()]
		public void OnlyAWaitingCommandOutlivesOneTickTest()
		{
			// The guarantee behind the change to CLIRequest: every other command is done when Execute
			// returns, exactly as before, because it inherits both members untouched.
			var deferring = new List<string>();

			foreach( var name in DirigentCommandRegistrator.CommandNames )
			{
				var type = TypeOf( name );

				var finished = type.GetProperty( "Finished", BindingFlags.Public | BindingFlags.Instance );
				Assert.IsNotNull( finished, $"{name} does not implement ICommand.Finished" );

				if( finished!.DeclaringType != typeof( Commands.DirigentControlCommand ) )
					deferring.Add( name );
			}

			CollectionAssert.AreEquivalent( new List<string>() { "GetAppFiles", "GetFiles", "DownloadPackage", "DownloadApp", "WaitForScript" , "GetAppLocation", "GetAppLocations" , "MarkFiles", "ClearFiles", "UnmarkFiles", "MarkAppFiles", "ClearAppFiles", "UnmarkAppFiles" }, deferring,
				"a command that reports itself unfinished keeps its request alive across ticks - "
				+ "intended for the commands that wait on something only" );
		}

		/// <summary>
		/// The commands taking several ids tell the ids from the options by name, so one id is the command it always was
		/// and an id never has to be quoted to be told from `machine=m1`.
		/// </summary>
		[TestMethod()]
		public void SeveralIdsAreToldFromTheOptions()
		{
			var args = new List<string>() { "pkg.a", "machine=m1", "pkg.b", "COMMENT=two words", "m1.app@plan", "wait=1" };

			CollectionAssert.AreEqual( new[] { "pkg.a", "pkg.b", "m1.app@plan" }, Commands.CliIds.Ids( args ) );

			var options = Commands.CliIds.Options( args );
			Assert.AreEqual( "m1", options["machine"] );
			Assert.AreEqual( "1", options["wait"] );
			Assert.AreEqual( 3, options.Count );

			CollectionAssert.AreEqual( new[] { "only" }, Commands.CliIds.Ids( new List<string>() { "only" } ) );
			Assert.AreEqual( 0, Commands.CliIds.Options( new List<string>() { "only" } ).Count );
		}

		/// <summary>The class implementing a command, via the terminator lookup's own table.</summary>
		static Type TypeOf( string commandName )
		{
			// the table is private, so this goes the way a caller would: the attribute lookup by name
			// must agree with the lookup by type
			var type = typeof( Commands.StartPlan ).Assembly
					.GetTypes()
					.Where( t => t.Namespace == "Dirigent.Commands" && !t.IsAbstract )
					.FirstOrDefault( t => t.Name == commandName );

			// the two names that do not match their class: LaunchApp is an alias of StartApp
			if( type is null && commandName == "LaunchApp" ) type = typeof( Commands.StartApp );

			Assert.IsNotNull( type, $"no class found for command '{commandName}'" );
			return type!;
		}
	}
}
