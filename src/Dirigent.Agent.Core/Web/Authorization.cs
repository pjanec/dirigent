using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

namespace Dirigent.Web
{
	/// <summary>
	/// What a token must allow to call this route of the web API. Every route method carries one - a test fails for one that does not - and the
	/// check at the door (<see cref="TokenAuthModule"/>) reads them. The scopes of a token are additive: <c>control</c> does not imply <c>read</c>.
	/// </summary>
	[AttributeUsage( AttributeTargets.Method, AllowMultiple = false )]
	public sealed class ScopeAttribute : Attribute
	{
		/// <summary>Every GET, and the one POST that only looks (<c>/files/query</c>).</summary>
		public const string Read = "read";

		/// <summary>Everything that acts: plans, apps, scripts, kill all, reload, variables, machine power, marks, downloads, the CLI.</summary>
		public const string Control = "control";

		/// <summary>Clearing files - the one thing that destroys data a test run left behind.</summary>
		public const string Sim = "sim";

		/// <summary>No token needed: what a caller that knows only the address asks first (the index and the manual).</summary>
		public const string Open = "open";

		/// <summary>The scopes a token must have, all of them. <see cref="Open"/> alone: none.</summary>
		public string[] Required { get; }

		public ScopeAttribute( params string[] required )
		{
			Required = required;
		}

		public bool IsOpen => Required.Length == 1 && Required[0] == Open;
	}

	/// <summary>
	/// The scope of every route, read from the controllers' own attributes: (verb, route template) to what a token must allow. A request that
	/// matches no route needs <c>control</c> - the safest reading of something nobody described.
	/// </summary>
	public static class ScopeMap
	{
		sealed record Entry( string Verb, string Template, string[] Segments, string[] Required, bool Open );

		static readonly Lazy<List<Entry>> _entries = new( Build );

		static List<Entry> Build()
		{
			var list = new List<Entry>();
			foreach( var type in typeof( ScopeMap ).Assembly.GetTypes().Where( t => typeof( WebApiController ).IsAssignableFrom( t ) && !t.IsAbstract ) )
				foreach( var method in type.GetMethods( BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly ) )
				{
					var scope = method.GetCustomAttribute<ScopeAttribute>();
					foreach( var route in method.GetCustomAttributes<RouteAttribute>() )
					{
						var template = route.Route == "/" ? "" : route.Route;
						list.Add( new Entry( route.Verb.ToString().ToUpperInvariant(), template, Split( template ),
							scope?.Required ?? new[] { ScopeAttribute.Control }, scope?.IsOpen ?? false ) );
					}
				}
			return list;
		}

		static string[] Split( string path ) => path.Split( '/', StringSplitOptions.RemoveEmptyEntries );

		/// <summary>One line per route, as GET /api lists them: verb, path as under /api, and the scopes it needs ("open" for none).</summary>
		public static IEnumerable<(string Verb, string Path, string Scope)> Routes()
			=> _entries.Value.Select( e => ( e.Verb, "/api" + e.Template, e.Open ? ScopeAttribute.Open : string.Join( " ", e.Required ) ) );

		/// <summary>What a request needs: the scopes of the route it matches, or <c>control</c> if it matches none.</summary>
		/// <param name="verb">GET, POST, DELETE ... (HEAD counts as GET)</param>
		/// <param name="absolutePath">the path as requested, "/api/plandefs/p1/start"; the part after "/api" is matched</param>
		public static (string[] Required, bool Open, bool Matched) Resolve( string verb, string absolutePath )
		{
			verb = verb.ToUpperInvariant();
			if( verb == "HEAD" ) verb = "GET";

			var path = absolutePath;
			if( path.StartsWith( "/api", StringComparison.OrdinalIgnoreCase ) ) path = path.Substring( 4 );
			var segments = Split( path );

			Entry? best = null;
			int bestLiterals = -1;

			foreach( var e in _entries.Value )
			{
				if( e.Verb != verb || e.Segments.Length != segments.Length ) continue;

				int literals = 0;
				bool ok = true;
				for( int i = 0; i < segments.Length && ok; i++ )
				{
					if( e.Segments[i].StartsWith( '{' ) ) continue;
					if( string.Equals( e.Segments[i], segments[i], StringComparison.OrdinalIgnoreCase ) ) literals++;
					else ok = false;
				}

				if( ok && literals > bestLiterals ) { best = e; bestLiterals = literals; }
			}

			return best is null ? ( new[] { ScopeAttribute.Control }, false, false ) : ( best.Required, best.Open, true );
		}
	}

	/// <summary>
	/// What the door learned about a request, for the controllers: whose token it was and what it allows.
	/// </summary>
	public sealed class AuthInfo
	{
		public ESecurityMode Mode { get; init; }
		public string Subject { get; init; } = "(no token)";
		public IReadOnlySet<string> Scopes { get; init; } = new HashSet<string>();
		public bool HasToken { get; init; }
		public string Address { get; init; } = "?";
	}

	public static class TokenAccess
	{
		internal const string ItemKey = "dirigent.auth";

		static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static AuthInfo? Of( IHttpContext context )
			=> context.Items.TryGetValue( ItemKey, out var v ) ? v as AuthInfo : null;

		/// <summary>
		/// For a route that needs more than its scope says - clearing files through POST /api/marks needs <c>sim</c> as well. With security off there is no
		/// door and nothing is asked; in audit mode a missing scope is logged and let through.
		/// </summary>
		/// <exception cref="HttpException">403, the token lacks the scope</exception>
		public static void Require( IHttpContext context, string scope )
		{
			var info = Of( context );
			if( info is null ) return;
			if( info.Scopes.Contains( scope ) ) return;

			var message = $"this operation needs the scope {scope}; the token has {( info.Scopes.Count == 0 ? "none" : string.Join( " ", info.Scopes ) )}";
			if( info.Mode == ESecurityMode.Audit )
			{
				log.Warn( $"audit: {info.Address} {context.Request.HttpMethod} {context.Request.Url.AbsolutePath} would be refused (403): {message}" );
				return;
			}
			throw new HttpException( 403, message );
		}
	}

	/// <summary>
	/// The door of the web server: nothing is answered before this has looked at the token. Registered after the banning of addresses and before everything else,
	/// so that no route, no web socket and no static file gets past it.
	/// </summary>
	/// <remarks>
	/// Open without a token: <c>GET /api</c>, <c>GET /api/help</c>, <c>GET /api/help/{doc}</c>, and the redirect of "/" to "/api". The web socket and the static
	/// folder need <c>read</c>. Every route under /api needs what its <see cref="ScopeAttribute"/> says, and a request that matches no route needs <c>control</c>.
	/// In audit mode all of it is checked and logged and nothing is refused.
	/// </remarks>
	public sealed class TokenAuthModule : WebModuleBase
	{
		static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		readonly SecuritySettings _security;
		readonly TokenValidator _validator;

		public TokenAuthModule( SecuritySettings security ) : base( "/" )
		{
			_security = security;
			_validator = security.Tokens ?? throw new InvalidOperationException( "no token validator: the master must have loaded the token public key" );
		}

		public override bool IsFinalHandler => false;

		protected override async Task OnRequestAsync( IHttpContext context )
		{
			var request = context.Request;
			var path = request.Url.AbsolutePath;
			var verb = request.HttpMethod.ToUpperInvariant();
			var remote = request.RemoteEndPoint?.Address;
			var address = remote is null ? "?" : ( remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote ).ToString();

			// what is needed
			string[] required;
			bool open = false;
			if( path.Equals( "/api", StringComparison.OrdinalIgnoreCase ) || path.StartsWith( "/api/", StringComparison.OrdinalIgnoreCase ) )
			{
				( required, open, _ ) = ScopeMap.Resolve( verb, path.TrimEnd( '/' ) );
			}
			else if( path == "/" && ( verb == "GET" || verb == "HEAD" ) )
			{
				open = true; // the redirect to /api
				required = Array.Empty<string>();
			}
			else
			{
				required = new[] { ScopeAttribute.Read }; // /websock and the static folder
			}

			// who is asking
			TokenResult? result = null;
			string? failure = null;
			var header = request.Headers["Authorization"];
			if( string.IsNullOrWhiteSpace( header ) )
			{
				failure = "no token: send Authorization: Bearer <token>";
			}
			else if( !header.StartsWith( "Bearer ", StringComparison.OrdinalIgnoreCase ) )
			{
				failure = "malformed token: the Authorization header must be 'Bearer <token>'";
			}
			else
			{
				result = _validator.Validate( header.Substring( 7 ) );
				if( !result.Ok ) failure = result.Error;
			}

			var scopes = result is { Ok: true } ? result.Scopes : new HashSet<string>();

			context.Items[TokenAccess.ItemKey] = new AuthInfo
			{
				Mode = _security.Mode,
				Subject = result is { Ok: true } ? ( result.Subject ?? "(no sub)" ) : "(no token)",
				Scopes = scopes,
				HasToken = result is { Ok: true },
				Address = address,
			};

			if( open ) return; // the way in for whoever knows only the address

			int refusal = 0;
			string? reason = null;
			if( failure is not null )
			{
				refusal = 401;
				reason = failure;
			}
			else
			{
				var missing = required.Where( r => !scopes.Contains( r ) ).ToList();
				if( missing.Count > 0 )
				{
					refusal = 403;
					reason = $"this route needs the scope {string.Join( " and ", required )}; the token has {( scopes.Count == 0 ? "none" : string.Join( " ", scopes ) )}";
				}
			}

			if( refusal != 0 )
			{
				if( _security.Mode == ESecurityMode.Audit )
				{
					log.Warn( $"audit: {address} {verb} {path} would be refused ({refusal}): {reason}" );
					// checked and logged, nothing refused: on with the call, which is written down below
				}
				else
				{

				log.Warn( $"{address} {verb} {path} refused ({refusal}): {reason}" );
				context.Response.StatusCode = refusal;
				context.Response.Headers.Add( "WWW-Authenticate", "Bearer" );
				var json = System.Text.Json.JsonSerializer.Serialize( new { error = reason, code = refusal } );
				await context.SendStringAsync( json, "application/json", ApiOps.Utf8 );
				context.SetHandled();
				return;
				}
			}

			// who stopped the plan: everything that changes something is written down with the subject of the token
			if( verb != "GET" && verb != "HEAD" )
				log.Info( $"{( result is { Ok: true } ? result.Subject ?? "(no sub)" : "(no token)" )} {verb} {path} from {address}" );
		}
	}
}
