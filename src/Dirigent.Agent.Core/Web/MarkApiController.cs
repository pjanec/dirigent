using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;

namespace Dirigent.Web
{
	/// <summary>
	/// Drawing a line under the log files before a test run, and taking it away: Mark, Clear and Unmark over REST.
	/// The work is the existing BuiltIns/MarkFiles.cs, ClearFiles.cs and UnmarkFiles.cs; this waits for them
	/// and answers with what happened to which file - see docs/MarkAndClear.md.
	/// </summary>
	public class MarkApiController : WebApiController
	{
		/// <summary>How long a call waits for the machines by default, and at most.</summary>
		public const int DefaultWaitSeconds = 60;
		public const int MaxWaitSeconds = 300;

		readonly Master _master;

		public MarkApiController( Master master )
		{
			_master = master;
		}

		// ?machine= and ?app= narrow the selection; {id} may be a wildcard (* = every clearable file)
		[Route( HttpVerbs.Post, "/filedefs/{id}/mark" )]
		public Task MarkFileDefs( string id ) => RunFileDefs( MarkOperation.EKind.Mark, id );

		[Route( HttpVerbs.Post, "/filedefs/{id}/clear" )]
		public Task ClearFileDefs( string id ) => RunFileDefs( MarkOperation.EKind.Clear, id );

		[Route( HttpVerbs.Post, "/filedefs/{id}/unmark" )]
		public Task UnmarkFileDefs( string id ) => RunFileDefs( MarkOperation.EKind.Unmark, id );

		// {id} is m1.a or m1.a@plan; the plan is picked as for the file previews
		[Route( HttpVerbs.Post, "/appdefs/{id}/mark" )]
		public Task MarkApp( string id ) => RunApp( MarkOperation.EKind.Mark, id );

		[Route( HttpVerbs.Post, "/appdefs/{id}/clear" )]
		public Task ClearApp( string id ) => RunApp( MarkOperation.EKind.Clear, id );

		[Route( HttpVerbs.Post, "/appdefs/{id}/unmark" )]
		public Task UnmarkApp( string id ) => RunApp( MarkOperation.EKind.Unmark, id );

		// body: {"operation":"mark|clear|unmark","fileDefs":[{"id":"pkg.a"}],"apps":["m1.a"]} - one operation over the union
		[Route( HttpVerbs.Post, "/marks" )]
		public async Task MarkSelection()
		{
			var body = await HttpContext.GetRequestBodyAsStringAsync();

			MarksRequest? request;
			try { request = System.Text.Json.JsonSerializer.Deserialize<MarksRequest>( body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true } ); }
			catch( Exception ) { throw HttpException.BadRequest( "The body is not a valid marks request." ); }
			if( request is null ) throw HttpException.BadRequest( "The body is not a valid marks request." );

			MarkOperation.EKind kind;
			switch( ( request.operation ?? string.Empty ).ToLowerInvariant() )
			{
				case "mark": kind = MarkOperation.EKind.Mark; break;
				case "clear": kind = MarkOperation.EKind.Clear; break;
				case "unmark": kind = MarkOperation.EKind.Unmark; break;
				default: throw HttpException.BadRequest( "'operation' must be mark, clear or unmark." );
			}

			await Run( () => MarkOperation.StartForSelection( _master, kind, FileSelection.Parse( _master, request ) ) );
		}

		/// <summary>The body of POST /api/marks.</summary>
		public class MarksRequest : FileSelectionRequest
		{
			public string? operation { get; set; }
		}

		async Task RunFileDefs( MarkOperation.EKind kind, string id )
		{
			await ApiOps.DiscardBody( HttpContext );
			var q = HttpContext.GetRequestQueryData();
			await Run( () => MarkOperation.StartForFileDefs( _master, kind, id, q["machine"], q["app"] ) );
		}

		async Task RunApp( MarkOperation.EKind kind, string id )
		{
			await ApiOps.DiscardBody( HttpContext );
			await Run( () => MarkOperation.StartForApp( _master, kind, id ) );
		}

		async Task Run( Func<MarkOperation> start )
		{
			var waitArg = HttpContext.GetRequestQueryData()["waitSeconds"];
			double waitSec = DefaultWaitSeconds;
			if( !string.IsNullOrEmpty( waitArg ) )
				double.TryParse( waitArg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out waitSec );
			waitSec = Math.Min( Math.Max( waitSec, 1 ), MaxWaitSeconds );

			// the definitions are picked and the script started on the master thread; waiting is not
			MarkOperation? operation = null;
			var op = _master.AddSynchronousOp( () => { operation = start(); } );
			await op.WaitAsync();

			if( op.Exception is AppFilesNotFoundException )
				throw HttpException.NotFound( op.Exception.Message );
			if( op.Exception is ArgumentException or ArgumentSyntaxErrorException )
				throw HttpException.BadRequest( op.Exception.Message );
			if( op.Exception != null ) throw op.Exception;

			var until = DateTime.UtcNow.AddSeconds( waitSec );
			Dirigent.ScriptState? state = null;

			while( true )
			{
				var poll = _master.AddSynchronousOp( () => { state = operation!.State( _master ); } );
				await poll.WaitAsync();

				if( state is not null && !state.IsAlive ) break;

				if( DateTime.UtcNow >= until )
					throw new HttpException( HttpStatusCode.GatewayTimeout,
						$"The {MarkOperation.Word( operation!.Kind )} has not finished in {waitSec:0} s; it keeps running as script {operation.ScriptId} - "
						+ $"poll it with GET /api/scriptstates/{operation.ScriptId}." );

				await Task.Delay( 200 );
			}

			MarkReport report;
			try
			{
				report = operation!.ReportOf( state! );
			}
			catch( Exception e )
			{
				throw new HttpException( HttpStatusCode.BadGateway, Tools.JustFirstLine( e.Message ) );
			}

			await HttpContext.SendStringAsync( report.ToJson(), "application/json", ApiOps.Utf8 );
		}
	}
}
