using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

using EmbedIO;

namespace Dirigent.Web
{
	/// <summary>
	/// What the control routes have in common: running something on the master's thread, saying what went wrong with the
	/// right status code, reading an optional JSON body, and waiting for a state to be reached.
	/// </summary>
	/// <remarks>
	/// The control routes are thin: each calls the very Master method the CLI command calls. What the master refuses it
	/// refuses with an exception, and its message is what the caller reads - a 409 when the operation is impossible in
	/// the state the system is in, 404 for something unknown, 400 for something that is not understood.
	/// </remarks>
	public static class ApiOps
	{
		/// <summary>How long a call may wait for a state, at most.</summary>
		public const int MaxWaitSeconds = 300;

		static readonly JsonSerializerOptions _readOptions = new() { PropertyNameCaseInsensitive = true };

		/// <summary>The exception of an operation as the HTTP error it is.</summary>
		public static Exception Map( Exception e )
		{
			switch( e )
			{
				case HttpException:
					return e;

				case UnknownPlanName or UnknownAppIdException or UnknownAppInPlanException or UnknownScriptId or AppFilesNotFoundException:
					return HttpException.NotFound( Tools.JustFirstLine( e.Message ) );

				case ArgumentException or ArgumentSyntaxErrorException or JsonException or FormatException or ConfigurationErrorException:
					return HttpException.BadRequest( Tools.JustFirstLine( e.Message ) );

				default:
					// the master declined: the operation is not possible as things stand
					return new HttpException( HttpStatusCode.Conflict, Tools.JustFirstLine( e.Message ) );
			}
		}

		/// <summary>Runs the action on the master's thread; what it throws becomes the HTTP error.</summary>
		public static async Task OnMaster( Master master, Action act )
		{
			var op = master.AddSynchronousOp( act );
			await op.WaitAsync();
			if( op.Exception != null ) throw Map( op.Exception );
		}

		public static async Task<T> OnMaster<T>( Master master, Func<T> fn )
		{
			T res = default!;
			await OnMaster( master, () => { res = fn(); } );
			return res;
		}

		/// <summary>The body of a request as JSON; null when there is none. A body that is not valid JSON is a 400.</summary>
		public static async Task<T?> ReadBody<T>( IHttpContext ctx ) where T : class
		{
			var text = await ctx.GetRequestBodyAsStringAsync();
			if( string.IsNullOrWhiteSpace( text ) ) return null;

			try { return JsonSerializer.Deserialize<T>( text, _readOptions ); }
			catch( Exception e ) { throw HttpException.BadRequest( "The body is not valid: " + Tools.JustFirstLine( e.Message ) ); }
		}

		/// <summary>The variables of a body, as the CLI takes them; null for none. A name or value the list cannot carry is a 400.</summary>
		public static Dictionary<string, string>? CheckVars( Dictionary<string, string>? vars )
		{
			if( vars is null || vars.Count == 0 ) return null;

			foreach( var (k, v) in vars )
			{
				if( string.IsNullOrWhiteSpace( k ) || k.Contains( '=' ) || k.Contains( "::" ) || ( v ?? "" ).Contains( "::" ) )
					throw HttpException.BadRequest( $"The variable '{k}' cannot be used: names are free of '=' and '::', values of '::'." );
			}
			return vars.ToDictionary( x => x.Key, x => x.Value ?? string.Empty );
		}

		/// <summary>?waitSeconds=N: how long to wait, 0 for not at all. Bounded by <see cref="MaxWaitSeconds"/>.</summary>
		public static double WaitSeconds( IHttpContext ctx )
		{
			var arg = ctx.GetRequestQueryData()["waitSeconds"];
			if( string.IsNullOrEmpty( arg ) ) return 0;

			if( !double.TryParse( arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sec ) || sec < 0 )
				throw HttpException.BadRequest( $"waitSeconds '{arg}' is not a number of seconds." );

			return Math.Min( sec, MaxWaitSeconds );
		}

		/// <summary>
		/// Polls the condition - evaluated on the master's thread - until it holds or the time is up.
		/// </summary>
		/// <returns>whether it held</returns>
		public static async Task<bool> WaitFor( Master master, Func<bool> condition, double seconds )
		{
			var until = DateTime.UtcNow.AddSeconds( seconds );

			while( true )
			{
				var ok = await OnMaster( master, condition );
				if( ok ) return true;
				if( DateTime.UtcNow >= until ) return false;
				await Task.Delay( 150 );
			}
		}

		public static HttpException Timeout( string what, double seconds )
			=> new HttpException( HttpStatusCode.GatewayTimeout, $"{what} did not happen in {seconds:0} s; it may still be going on." );
	}
}
