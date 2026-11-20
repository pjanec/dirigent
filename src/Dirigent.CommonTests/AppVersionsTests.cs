using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Web;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// The versions of a site, each listed once (GET /api/appversions): applications grouped by component, source and
	/// text - and the text left as it is, only split into lines.
	/// </summary>
	[TestClass()]
	public class AppVersionsTests
	{
		static AppLocationEntry Loc( string app, string? component, string? source, string? version, bool exists = true, string? error = null )
			=> new AppLocationEntry()
			{
				app = app,
				machine = app.Split( '.' )[0],
				component = component,
				kind = "process",
				error = error,
				exe = new AppLocationPath() { exists = exists, versionSource = source, fileVersion = version },
			};

		const string Stamp = "Build Number - 20261110.9\nViewer Version - 1.2.470.218036\nBranch Name - refs/heads/Development";

		[TestMethod()]
		public void TheSameVersionOnSeveralMachinesIsListedOnceTest()
		{
			var versions = AppVersions.Of( new[]
			{
				Loc( "m1.sim", "Viewer", @"C:\Viewer\VersionStamp.txt", Stamp ),
				Loc( "m2.sim", "Viewer", @"C:\Viewer\VersionStamp.txt", Stamp ),
				Loc( "m2.player", "Viewer", @"C:\Viewer\VersionStamp.txt", Stamp ),
			} );

			Assert.AreEqual( 1, versions.Count );
			CollectionAssert.AreEqual( new[] { "m1", "m2" }, versions[0].machines );
			CollectionAssert.AreEqual( new[] { "m1.sim", "m2.player", "m2.sim" }, versions[0].apps );

			// the text as it is, in lines - nothing extracted from it
			CollectionAssert.AreEqual( Stamp.Split( '\n' ), versions[0].version );
			Assert.IsNull( versions[0].missing );
		}

		[TestMethod()]
		public void ADifferentTextIsADifferentVersionTest()
		{
			// one machine was not updated: that is what the list has to show at a glance
			var versions = AppVersions.Of( new[]
			{
				Loc( "m1.sim", "Viewer", "resource", "1.2.470.218036" ),
				Loc( "m2.sim", "Viewer", "resource", "1.2.469.217000" ),
			} );

			Assert.AreEqual( 2, versions.Count );
			Assert.IsTrue( versions.All( v => v.machines.Count == 1 ) );
		}

		[TestMethod()]
		public void WhatHasNoVersionIsListedWithTheReasonTest()
		{
			var versions = AppVersions.Of( new[]
			{
				Loc( "m1.render", "Render", "resource", "1.0.0.337" ),
				Loc( "m1.gone", "Render", null, null, exists: false ),
				Loc( "m1.plain", "Render", null, null ),
				Loc( "m9.away", "Render", null, null, exists: false, error: "Machine m9 not connected." ),
				Loc( "m1.loose", null, "resource", "7.0" ),
			} );

			// a component's versions first, then what has none; applications naming no component at the end
			CollectionAssert.AreEqual( new[] { "m1.render", "m9.away", "m1.plain", "m1.gone", "m1.loose" },
				versions.Select( v => v.apps.Single() ).ToList(), string.Join( " | ", versions.Select( v => $"{v.component}/{v.missing}" ) ) );

			Assert.AreEqual( "Machine m9 not connected.", versions[1].missing );
			Assert.AreEqual( "the program carries no version, and there is no version file next to it", versions[2].missing );
			Assert.AreEqual( "the program is not there", versions[3].missing );
			Assert.IsNull( versions[2].version );
			Assert.IsNull( versions[4].component );
		}
	}
}
