using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Dirigent.Tests
{
	/// <summary>
	/// docs/HTTP.md describes every route of the web API, and only those.
	/// </summary>
	/// <remarks>
	/// The convention that makes this checkable: each endpoint has a heading of the form
	/// <c>### GET /api/appdefs/{id}</c> (or <c>####</c>) - the verb and the route template exactly as the controller declares
	/// it, with the parameters as <c>{name}</c>; examples with real ids belong in the body. A query string after
	/// the path (<c>?wait=N</c>) is ignored. The page is served by every master at GET /api/help, so a route
	/// added without a heading - or a heading left behind - is a page that lies.
	/// </remarks>
	[TestClass()]
	public class HttpDocDriftTests
	{
		static readonly Regex _heading = new( @"^#{3,4}\s+(GET|POST|PUT|DELETE|PATCH)\s+(/api\S*?)(\?\S*)?(\s.*)?$" );

		static string HttpMd()
		{
			var dir = new DirectoryInfo( AppContext.BaseDirectory );
			while( dir is not null && !File.Exists( Path.Combine( dir.FullName, "version.txt" ) ) )
				dir = dir.Parent;

			Assert.IsNotNull( dir, "could not find the repository root above " + AppContext.BaseDirectory );
			return Path.Combine( dir!.FullName, "docs", "HTTP.md" );
		}

		/// <summary>"GET /api/appdefs/{id}" for every [Route] of every web controller.</summary>
		static SortedSet<string> RoutesOfTheControllers()
		{
			var routes = new SortedSet<string>( StringComparer.Ordinal );

			var controllers = typeof( Web.AppApiController ).Assembly.GetTypes()
				.Where( t => typeof( EmbedIO.WebApi.WebApiController ).IsAssignableFrom( t ) && !t.IsAbstract );

			foreach( var type in controllers )
			{
				foreach( var method in type.GetMethods( BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly ) )
				{
					foreach( var attr in method.GetCustomAttributes<EmbedIO.Routing.RouteAttribute>() )
					{
						var verb = attr.Verb.ToString().ToUpperInvariant();
						routes.Add( $"{verb} /api{attr.Route}" );
					}
				}
			}

			return routes;
		}

		static SortedSet<string> HeadingsOfTheDocument()
		{
			var headings = new SortedSet<string>( StringComparer.Ordinal );
			var inFence = false;

			foreach( var line in File.ReadAllLines( HttpMd() ) )
			{
				if( line.TrimStart().StartsWith( "```" ) ) { inFence = !inFence; continue; }
				if( inFence ) continue;

				var m = _heading.Match( line );
				if( m.Success ) headings.Add( $"{m.Groups[1].Value} {m.Groups[2].Value}" );
			}

			return headings;
		}

		[TestMethod()]
		public void EveryRouteHasAHeadingInHttpMdAndEveryHeadingARouteTest()
		{
			var routes = RoutesOfTheControllers();
			var headings = HeadingsOfTheDocument();

			Assert.IsTrue( routes.Count > 20, $"only {routes.Count} routes found - did the controllers move?" );

			var undocumented = routes.Except( headings ).ToList();
			var unknown = headings.Except( routes ).ToList();

			Assert.AreEqual( 0, undocumented.Count + unknown.Count,
				"docs/HTTP.md has drifted from the controllers.\n"
				+ "routes without a heading (add '### VERB /api/route'):\n  " + string.Join( "\n  ", undocumented )
				+ "\nheadings without a route (remove or fix; use the route template with {name} parameters):\n  " + string.Join( "\n  ", unknown ) );
		}
	}
}
