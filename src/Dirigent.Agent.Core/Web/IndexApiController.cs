using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;

namespace Dirigent.Web
{
	public class ApiRoute
	{
		public string method { get; set; } = string.Empty;
		public string path { get; set; } = string.Empty;

		/// <summary>The section of the manual describing the route; the manual itself if it has none.</summary>
		public string help { get; set; } = string.Empty;

		/// <summary>The scopes a token must allow, all of them: read, control, sim - or open for a route that needs no token.</summary>
		public string scope { get; set; } = string.Empty;
	}

	/// <summary>What GET /api answers: what this is, where to begin, and every route there is.</summary>
	public class ApiIndex
	{
		public string name { get; set; } = "Dirigent";
		public string version { get; set; } = string.Empty;
		public string? siteName { get; set; }
		public string masterMachineId { get; set; } = string.Empty;
		public string? sharedConfig { get; set; }

		/// <summary>The reference of this API, as Markdown.</summary>
		public string help { get; set; } = string.Empty;

		/// <summary>
		/// Whether the API asks for a token: <c>off</c> (open), <c>audit</c> (checked and logged, nothing refused) or <c>on</c>.
		/// With audit or on, <c>Authorization: Bearer &lt;JWT&gt;</c> (ES256) and each route says in <c>scope</c> what the token must allow.
		/// </summary>
		public string authentication { get; set; } = "off";

		/// <summary>Every page of the manual of this version.</summary>
		public List<string> docs { get; set; } = new();

		/// <summary>The order of questions for finding out what the site is, before doing anything.</summary>
		public List<string> start { get; set; } = new();

		public string limits { get; set; } = string.Empty;

		public List<ApiRoute> routes { get; set; } = new();
	}

	/// <summary>
	/// The front door: GET /api says what answers here, for a tool or an AI agent that knows only the address.
	/// </summary>
	/// <remarks>
	/// The routes are read from the controllers' own [Route] attributes, so the list cannot be out of date; each points
	/// at its section of the manual (HTTP.md, from the same assembly) - a route without one points at the manual as a
	/// whole, which a test treats as a gap in the documentation.
	/// </remarks>
	public class IndexApiController : WebApiController
	{
		readonly Master _master;

		public IndexApiController( Master master )
		{
			_master = master;
		}

		const string Manual = "/api/help/HTTP.md";

		[Scope( ScopeAttribute.Open )]
		[Route( HttpVerbs.Get, "/" )]
		public async Task GetIndex()
		{
			var index = await ApiOps.OnMaster( _master, () => new ApiIndex()
			{
				version = AppConfig.VersionText(),
				siteName = string.IsNullOrEmpty( _master.SiteName ) ? null : _master.SiteName,
				masterMachineId = _master.MachineId,
				sharedConfig = _master.SharedConfigFile,
			} );

			index.help = Manual;
			index.authentication = _master.Security.Mode.ToString().ToLowerInvariant();
			index.docs = HelpApiController.Pages.Select( p => "/api/help/" + p ).ToList();
			index.start = new()
			{
				"GET /api/status - which Dirigent this is, what it runs, who is connected",
				"GET /api/machinedefs - the machines: address, online, shares, their apps and file definitions",
				"GET /api/appdefs, /api/appstates, /api/applocations - the applications, what they do, where their binaries are",
				"GET /api/plandefs, /api/planstates - the launch plans",
				"GET /api/filedefs?type=FilePackage - what files there is to look at, collect or mark; 'component' says which component a package stands for",
				"GET /api/scriptdefs - the scripts the configuration defines",
			};
			index.limits = $"More than {WebServerRunner.MaxRequestsPerSecond} requests in a second ban the address for "
				+ $"{WebServerRunner.BanMinutes} minutes - ask for lists (/api/appstates, ...), not one item at a time. "
				+ ( _master.Security.Mode == ESecurityMode.Off
					? "No authentication: anyone reaching this port can control the site."
					: "Every call but this index and the manual needs a token: Authorization: Bearer <JWT> (ES256), with the scope the route lists - see /api/help/Security.md." );
			index.routes = Routes();

			await HttpContext.SendStringAsync( AppFilesResult.ToJson( index ), "application/json", ApiOps.Utf8 );
		}

		/// <summary>Every route of the API, ordered by path, with its section of the manual.</summary>
		public static List<ApiRoute> Routes()
		{
			var headings = ManualHeadings();

			return ( from type in typeof( IndexApiController ).Assembly.GetTypes()
					 where typeof( WebApiController ).IsAssignableFrom( type ) && !type.IsAbstract
					 from method in type.GetMethods( BindingFlags.Instance | BindingFlags.Public )
					 from attr in method.GetCustomAttributes<RouteAttribute>()
					 let path = "/api" + ( attr.Route == "/" ? "" : attr.Route )
					 let verb = attr.Verb.ToString().ToUpperInvariant()
					 let scopeAttr = method.GetCustomAttribute<ScopeAttribute>()
					 orderby path, verb
					 select new ApiRoute() { method = verb, path = path, help = HelpFor( verb, path, headings ), scope = scopeAttr is null ? ScopeAttribute.Control : string.Join( " ", scopeAttr.Required ) } ).ToList();
		}

		/// <summary>The headings of HTTP.md that describe a route: "GET /api/downloads/{id}?wait=N" and the like.</summary>
		static List<string> ManualHeadings()
		{
			var text = HelpApiController.ReadPage( "HTTP.md" ) ?? string.Empty;
			return ( from line in text.Split( '\n' )
					 let m = Regex.Match( line.TrimEnd( '\r' ), @"^#{2,4}\s+((GET|POST|PUT|DELETE)\s+/\S*)\s*$" )
					 where m.Success
					 select m.Groups[1].Value ).ToList();
		}

		/// <summary>
		/// The link to the heading of the route: "GET /api/a/{id}" itself, or followed by its query ("?wait=N"). The anchor is
		/// the heading without spaces and with only letters, digits and dashes - the form the manual's own links use.
		/// </summary>
		static string HelpFor( string verb, string path, List<string> headings )
		{
			var heading = headings.FirstOrDefault( h => h == $"{verb} {path}" || h.StartsWith( $"{verb} {path}?", StringComparison.Ordinal ) );
			if( heading is null ) return Manual;

			var anchor = Regex.Replace( heading.Replace( ' ', '-' ), "[^A-Za-z0-9-]", "" );
			return $"{Manual}#{anchor}";
		}
	}
}
