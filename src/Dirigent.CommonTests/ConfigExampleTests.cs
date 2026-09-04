using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// That the example configuration in the repository is a configuration that works.
	/// </summary>
	/// <remarks>
	/// `config/SharedConfig.xml` ships in the release archive and is what people copy from, so an
	/// example in it that does not load, or that loads and then does something other than what its
	/// comment claims, is worse than no example at all.
	///
	/// The `[dirigent.command]` step is the one worth pinning. Its command line passes through two
	/// unrelated levels of quoting - XML attribute, then the CLI word tokenizer - and the two escape
	/// differently, so a line that looks right in a document can be either invalid XML or a valid
	/// attribute holding the wrong command. The version first written into the documentation was the
	/// former: it doubled the double quotes the way the tokenizer wants, inside an XML attribute
	/// delimited by double quotes, so the file did not parse at all.
	/// </remarks>
	[TestClass()]
	public class ConfigExampleTests
	{
		static string RepoRoot()
		{
			// walk up from the test binaries until the repository root shows itself
			var dir = new DirectoryInfo( AppContext.BaseDirectory );

			while( dir is not null && !File.Exists( Path.Combine( dir.FullName, "version.txt" ) ) )
				dir = dir.Parent;

			Assert.IsNotNull( dir, "could not find the repository root above " + AppContext.BaseDirectory );
			return dir!.FullName;
		}

		static string SharedConfigPath => Path.Combine( RepoRoot(), "config", "SharedConfig.xml" );

		[TestMethod()]
		public void TheExampleSharedConfigReallyLoadsTest()
		{
			// Through the real reader, not just as XML: well-formed is not the same as loadable, and
			// the reader also checks the plans' dependencies. This is the shipped example - if it
			// does not load, every reader who copies from it starts by debugging our file.
			using var text = new StreamReader( SharedConfigPath );
			var cfg = new SharedConfigReader( text ).Config;

			Assert.IsTrue( cfg.Plans.Count > 0, "no plans were read" );
			Assert.IsTrue( cfg.VfsNodes.Count > 0, "no file nodes were read" );

			// the pieces the new example is made of
			var marked = cfg.VfsNodes.Where( n => n.Clearable ).ToList();
			Assert.IsTrue( marked.Count > 0, "the example declares no Clearable node" );
			Assert.IsTrue( marked.Any( n => n.TailBytes > 0 ), "the example declares no TailBytes" );

			// The reader validates this one itself - ValidateInitConditions refuses 'cliresponse' on
			// an app that is not a dirigent command, and refuses it without ok|any - so merely
			// getting here proves the example's condition is a legal one.
			Assert.IsTrue(
				cfg.Plans.Any( p => p.AppDefs.Any(
					a => ( a.InitializedCondition ?? "" ).StartsWith( InitConditions.CliResponse,
							StringComparison.OrdinalIgnoreCase ) ) ),
				"no plan step waits for a dirigent command" );
		}

		/// <summary>Every app of the example config that issues a Dirigent command.</summary>
		static List<XElement> DirigentCommandSteps()
			=> XDocument.Load( SharedConfigPath )
					.Descendants( "App" )
					.Where( a => ( a.Attribute( "ExeFullPath" )?.Value ?? "" )
							.Equals( ReservedExeNames.DirigentCommand, StringComparison.OrdinalIgnoreCase ) )
					.ToList();

		[TestMethod()]
		public void ADirigentCommandStepSurvivesBothLevelsOfQuotingTest()
		{
			var steps = DirigentCommandSteps();

			Assert.IsTrue( steps.Count > 0,
				$"the example config no longer shows a {ReservedExeNames.DirigentCommand} step; if that "
				+ "was deliberate, drop this test with it" );

			foreach( var step in steps )
			{
				var who = step.Attribute( "AppIdTuple" )?.Value ?? "?";
				var cmdLine = step.Attribute( "CmdLineArgs" )?.Value;

				Assert.IsFalse( string.IsNullOrWhiteSpace( cmdLine ), $"{who} sends no command at all" );

				// the master splits the line on semicolons, and so does the response tracker
				var commands = cmdLine!.Split( ';' ).Select( x => x.Trim() ).Where( x => x.Length > 0 ).ToList();
				Assert.IsTrue( commands.Count > 0, $"{who}: nothing to send" );

				foreach( var command in commands )
				{
					CommandRepository.SplitToWordTokens( command, out var tokens );

					Assert.IsTrue( tokens.Count > 0, $"{who}: '{command}' tokenizes to nothing" );

					// every command of the line must be one Dirigent knows, or the step reports a
					// failure the plan then has to interpret
					var name = tokens[0];
					Assert.IsTrue(
						DirigentCommandRegistrator.CommandNames.Any(
							x => x.Equals( name, StringComparison.Ordinal ) ),
						$"{who}: '{name}' is not a Dirigent command (names are case sensitive)" );

					// a JSON argument has to arrive as ONE token and keep its quotes: the tokenizer
					// strips a single quote it treats as a delimiter, so '{Id:'x'}' would reach the
					// script as {Id:x} and fail to deserialize
					var json = tokens.FirstOrDefault( t => t.StartsWith( "{" ) );
					if( json is not null )
					{
						StringAssert.EndsWith( json, "}", $"{who}: the JSON argument arrived cut short: {json}" );

						Assert.AreEqual( json.Count( c => c == '{' ), json.Count( c => c == '}' ),
							$"{who}: unbalanced braces in the argument that reaches the script: {json}" );

						// it must still be parseable after both levels of unquoting
						var parsed = Tools.Deserialize<Scripts.BuiltIn.MarkOrClearFiles.TArgs>( json );
						Assert.IsNotNull( parsed, $"{who}: the script cannot deserialize {json}" );
						Assert.IsFalse( string.IsNullOrEmpty( parsed!.Node?.Id ),
							$"{who}: the argument names no node after unquoting: {json}" );
					}
				}
			}
		}

		[TestMethod()]
		public void AWaitingStepActuallyWaitsForWhatItStartedTest()
		{
			// The point of the example: the plan must not carry on before the mark is drawn. That
			// needs the cliresponse condition AND a WaitForScript naming the same guid the
			// StartScript used - a mismatch would wait for a script nobody started.
			foreach( var step in DirigentCommandSteps() )
			{
				var who = step.Attribute( "AppIdTuple" )?.Value ?? "?";
				var cmdLine = step.Attribute( "CmdLineArgs" )?.Value ?? "";

				if( !cmdLine.Contains( "WaitForScript", StringComparison.Ordinal ) ) continue;

				var condition = step.Attribute( "InitCondition" )?.Value ?? "";
				StringAssert.StartsWith( condition, InitConditions.CliResponse,
					$"{who} waits for a script but its InitCondition is '{condition}', so the plan "
					+ "would carry on the moment the step was launched" );

				// the guids of StartScript and WaitForScript agree
				var guids = new List<Guid>();
				foreach( var command in cmdLine.Split( ';' ) )
				{
					CommandRepository.SplitToWordTokens( command.Trim(), out var tokens );
					foreach( var token in tokens )
						if( Guid.TryParse( token, out var g ) ) guids.Add( g );
				}

				Assert.IsTrue( guids.Count >= 2, $"{who}: expected a guid on both commands" );
				Assert.AreEqual( 1, guids.Distinct().Count(),
					$"{who}: StartScript and WaitForScript name different scripts, so the step waits "
					+ "for something nobody started" );
			}
		}

		[TestMethod()]
		public void TheExampleShowsTheAttributesTheReleaseAddedTest()
		{
			// the example config is the only thing a user receives that can show these; the docs are
			// not in the release archive
			var text = File.ReadAllText( SharedConfigPath );

			foreach( var attribute in new[] { "Clearable", "TailBytes", "AskComment", "cliresponse" } )
			{
				StringAssert.Contains( text, attribute,
					$"nothing in the example configuration demonstrates {attribute}" );
			}
		}
	}
}
