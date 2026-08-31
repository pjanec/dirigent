using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.IO;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// What the config reader accepts as `InitCondition="cliresponse …"`, and what it refuses to load.
	/// </summary>
	/// <remarks>
	/// The condition waits for the master's answer to a Dirigent command, so on an app that sends no
	/// such command it could never be satisfied - it would hold that app, and everything depending on
	/// it, for ever. A refusal at load time is the kinder outcome: on startup the master stops with a
	/// reason, and on a reload the previous config stays in effect while the requestor is told why.
	///
	/// Nothing in the field can trip over this: the condition is new, so no existing config uses it.
	/// </remarks>
	[TestClass()]
	public class CliResponseConditionTests
	{
		static SharedConfig Parse( string xml )
			=> new SharedConfigReader( new StringReader( xml ) ).Config;

		static string ConfigWith( string appAttributes, string appElements = "" )
			=> $@"
				<Shared>
					<Machine Name='m1' IP='127.0.0.1'/>
					<Plan Name='p'>
						<App AppIdTuple='m1.step' {appAttributes}>{appElements}</App>
					</Plan>
				</Shared>";

		[TestMethod()]
		public void ADirigentCommandStepWithAValueLoadsTest()
		{
			foreach( var value in new[] { "ok", "any", "OK", "Any" } )
			{
				var cfg = Parse( ConfigWith(
					$@"ExeFullPath='[dirigent.command]' CmdLineArgs='StartPlan other'
					   InitCondition='cliresponse {value}'" ) );

				var app = cfg.Plans.Single().AppDefs.Single();
				Assert.AreEqual( $"cliresponse {value}", app.InitializedCondition );
			}
		}

		[TestMethod()]
		public void TheReservedExeNameIsMatchedIgnoringCaseTest()
		{
			// as Launcher.ParseExe matches it
			var cfg = Parse( ConfigWith(
				@"ExeFullPath='[Dirigent.Command]' CmdLineArgs='StartPlan other'
				  InitCondition='cliresponse ok'" ) );

			Assert.AreEqual( 1, cfg.Plans.Single().AppDefs.Count );
		}

		[TestMethod()]
		public void AnOrdinaryApplicationCannotUseItTest()
		{
			var ex = Assert.ThrowsException<Exception>( () => Parse( ConfigWith(
				@"ExeFullPath='c:\windows\notepad.exe' InitCondition='cliresponse ok'" ) ) );

			StringAssert.Contains( ex.Message, "m1.step", "the app has to be named" );
			StringAssert.Contains( ex.Message, "[dirigent.command]", "and the reason given" );
		}

		[TestMethod()]
		public void TheValueIsMandatoryTest()
		{
			// "wait for it" and "wait for it to succeed" are different enough decisions that a config
			// has to make one of them out loud
			var ex = Assert.ThrowsException<Exception>( () => Parse( ConfigWith(
				@"ExeFullPath='[dirigent.command]' CmdLineArgs='StartPlan other'
				  InitCondition='cliresponse'" ) ) );

			StringAssert.Contains( ex.Message, "needs a value" );
		}

		[TestMethod()]
		public void AnUnknownValueIsRefusedTest()
		{
			var ex = Assert.ThrowsException<Exception>( () => Parse( ConfigWith(
				@"ExeFullPath='[dirigent.command]' CmdLineArgs='StartPlan other'
				  InitCondition='cliresponse maybe'" ) ) );

			StringAssert.Contains( ex.Message, "maybe" );
		}

		[TestMethod()]
		public void TheElementFormIsCheckedTooTest()
		{
			// the form that lets a step combine conditions: <cliresponse>any</cliresponse> alongside a
			// <timeout>, which is how one gets a ceiling on waiting
			var cfg = Parse( ConfigWith(
				@"ExeFullPath='[dirigent.command]' CmdLineArgs='StartPlan other'",
				@"<InitDetectors><cliresponse>any</cliresponse><timeout>60</timeout></InitDetectors>" ) );

			var app = cfg.Plans.Single().AppDefs.Single();
			Assert.AreEqual( 2, app.InitDetectors.Count );

			// and the same mistake in that form is caught as well
			var ex = Assert.ThrowsException<Exception>( () => Parse( ConfigWith(
				@"ExeFullPath='c:\windows\notepad.exe'",
				@"<InitDetectors><cliresponse>ok</cliresponse></InitDetectors>" ) ) );

			StringAssert.Contains( ex.Message, "[dirigent.command]" );
		}

		[TestMethod()]
		public void OtherInitConditionsAreLeftAloneTest()
		{
			// the validation must not have opinions about anything else, including on a
			// [dirigent.command] app - where exitcode 0 has always initialized the step at once
			foreach( var condition in new[] { "exitcode 0", "exitcode 0,1", "timeout 2.5" } )
			{
				var cfg = Parse( ConfigWith(
					$@"ExeFullPath='[dirigent.command]' CmdLineArgs='StartPlan other'
					   InitCondition='{condition}'" ) );

				Assert.AreEqual( condition, cfg.Plans.Single().AppDefs.Single().InitializedCondition );
			}

			var normal = Parse( ConfigWith( @"ExeFullPath='c:\windows\notepad.exe' InitCondition='exitcode 0'" ) );
			Assert.AreEqual( "exitcode 0", normal.Plans.Single().AppDefs.Single().InitializedCondition );
		}
	}
}
