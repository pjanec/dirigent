using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;

namespace Dirigent.Tests
{
	/// <summary>
	/// The launch spec was moved out of the launcher unchanged. These pin it: each expected string below was
	/// worked out by hand from the code the launcher had before the move.
	/// </summary>
	[TestClass()]
	public class AppLaunchSpecTests
	{
		const string Root = @"C:\root";

		static AppDef App( string exe, string args = "", string startupDir = "" )
		{
			var app = new AppDef()
			{
				Id = new AppIdTuple( "m1", "a" ),
				ExeFullPath = exe,
				CmdLineArgs = args,
				StartupDir = startupDir,
			};
			app.PlanName = "plan1";
			return app;
		}

		static AppLaunchSpec Spec( AppDef app, Dictionary<string, string>? extra = null )
			=> new AppLaunchSpec( app, new Dictionary<string, string>() { { "SUB", "sub" }, { "SHARED", "global" } }, extra, "10.0.0.1", Root );

		[TestMethod()]
		public void APlainExeWithRelativePathsAndVariablesTest()
		{
			Environment.SetEnvironmentVariable( "DIRIGENT_SPEC_TEST", "tools" );

			var spec = Spec( App( @"%DIRIGENT_SPEC_TEST%\%SUB%\app.exe", "--id %APP_ID% --on %MACHINE_ID% --bin %APP_BINDIR% --v %SHARED%", @"work\%MACHINE_ID%" ) );
			var pe = spec.ParseExe();

			Assert.AreEqual( EExeType.Executable, pe.ExeType );
			Assert.IsNull( pe.ReservedName );
			Assert.AreEqual( @"tools\sub\app.exe", pe.Path );

			// APP_BINDIR is expanded with the app's own variables only, so the internal SUB is gone: "tools\" is what is left
			Assert.AreEqual( @"--id a --on m1 --bin tools\ --v global", pe.CmdLine );

			Assert.AreEqual( @"C:\root\tools\sub\app.exe", spec.GetFileName( pe ) );
			Assert.AreEqual( @"C:\root\work\m1", spec.GetWorkingDirectory() );
		}

		[TestMethod()]
		public void AnAbsoluteExeIsLeftAsItIsTest()
		{
			var spec = Spec( App( @"D:\apps\x.exe", "", @"D:\apps" ) );
			var pe = spec.ParseExe();

			Assert.AreEqual( @"D:\apps\x.exe", spec.GetFileName( pe ) );
			Assert.AreEqual( @"D:\apps", spec.GetWorkingDirectory() );
			Assert.AreEqual( "", pe.CmdLine );
		}

		[TestMethod()]
		public void ReservedCmdNamesGetTheShellAndTheirPrefixTest()
		{
			var comSpec = Environment.GetEnvironmentVariable( "ComSpec" ) ?? "";

			foreach( var (name, expectedReserved, expectedPrefix) in new[] { ( "[cmd.command]", "cmd.command", "/c " ), ( "[CMD.FILE]", "cmd.file", "/c " ), ( "[cmd]", "cmd", "" ) } )
			{
				var spec = Spec( App( name, "echo %APP_ID%" ) );
				var pe = spec.ParseExe();

				Assert.AreEqual( EExeType.Executable, pe.ExeType, name );
				Assert.AreEqual( expectedReserved, pe.ReservedName, name );
				Assert.AreEqual( comSpec, pe.Path, name );
				Assert.AreEqual( expectedPrefix + "echo a", pe.CmdLine, name );
			}
		}

		[TestMethod()]
		public void ReservedPowershellNamesGetTheirPrefixTest()
		{
			var ps = Environment.ExpandEnvironmentVariables( @"%windir%\system32\WindowsPowerShell\v1.0\powershell.exe" );

			var file = Spec( App( "[powershell.file]", @"x.ps1 %APP_ID%" ) ).ParseExe();
			Assert.AreEqual( ps, file.Path );
			Assert.AreEqual( "powershell.file", file.ReservedName );
			Assert.AreEqual( "-executionpolicy bypass -file x.ps1 a", file.CmdLine );

			var command = Spec( App( "[powershell.command]", "Get-Date" ) ).ParseExe();
			Assert.AreEqual( "-executionpolicy bypass -command \"Get-Date\"", command.CmdLine );

			var plain = Spec( App( "[powershell]", "-NoProfile" ) ).ParseExe();
			Assert.AreEqual( "-NoProfile", plain.CmdLine );
		}

		[TestMethod()]
		public void ADirigentCommandRunsNoProgramTest()
		{
			var spec = Spec( App( "[dirigent.command]", "StartApp m1.b" ) );
			var pe = spec.ParseExe();

			Assert.AreEqual( EExeType.DirigentCmd, pe.ExeType );
			Assert.AreEqual( "dirigent.command", pe.ReservedName );
			Assert.AreEqual( "", pe.Path );
			Assert.AreEqual( "StartApp m1.b", pe.CmdLine );
		}

		[TestMethod()]
		public void TheVariablesKeepTheirOrderAndPrecedenceTest()
		{
			var app = App( @"x.exe" );
			app.LocalVarsToSet["SHARED"] = "local";
			app.LocalVarsToSet["ONLYLOCAL"] = "l";

			var spec = Spec( app, new Dictionary<string, string>() { { "ONLYLOCAL", "extra" }, { "FROMSTART", "s" } } );

			// the global one, overridden by the app's own, overridden by what was given at the start
			Assert.AreEqual( "local", spec.ExpansionVars["SHARED"] );
			Assert.AreEqual( "extra", spec.ExpansionVars["ONLYLOCAL"] );
			Assert.AreEqual( "s", spec.ExpansionVars["FROMSTART"] );
			Assert.AreEqual( "sub", spec.ExpansionVars["SUB"] );

			// what the launcher publishes into the environment of the process
			Assert.AreEqual( "plan1", spec.PublishedVars["DIRIGENT_PLAN"] );
			Assert.AreEqual( "m1", spec.PublishedVars["DIRIGENT_MACHINEID"] );
			Assert.AreEqual( "a", spec.PublishedVars["DIRIGENT_APPID"] );
			Assert.AreEqual( "10.0.0.1", spec.PublishedVars["DIRIGENT_MASTER_IP"] );
			Assert.AreEqual( "s", spec.PublishedVars["FROMSTART"] );
			Assert.IsFalse( spec.PublishedVars.ContainsKey( "SUB" ), "the global variables are for expansion only" );
		}
	}
}
