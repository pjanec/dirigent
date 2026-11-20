using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;

namespace Dirigent.Web
{
	/// <summary>
	/// The node listing and the download jobs. A diagnostic tool lists the nodes, previews the files
	/// (GET /api/appdefs/{id}/files, /api/filedefs/{id}/files), starts a job, polls it and fetches the archive.
	/// </summary>
	public class DownloadApiController : WebApiController
	{
		readonly Master _master;

		public DownloadApiController( Master master )
		{
			_master = master;
		}

		// the type is compared case-insensitively; the other filters take the wildcards of ListVfsNodes
		[Route( HttpVerbs.Get, "/filedefs" )]
		public async Task GetFileDefs()
		{
			var q = HttpContext.GetRequestQueryData();

			List<FileDefInfo> nodes = new();
			var op = _master.AddSynchronousOp( () =>
			{
				nodes = FileDefListing.List( _master, q["type"], q["id"], q["machine"], q["app"], q["group"] );
			} );
			await op.WaitAsync();
			if( op.Exception is ArgumentException ) throw HttpException.BadRequest( op.Exception.Message );
			if( op.Exception != null ) throw op.Exception;

			await SendJson( FileDefListing.ToJson( nodes ) );
		}

		// the definitions with this id (wildcards allowed), narrowed by ?machine= and ?app=; the listing shape
		[Route( HttpVerbs.Get, "/filedefs/{id}" )]
		public async Task GetFileDef( string id )
		{
			var q = HttpContext.GetRequestQueryData();

			List<FileDefInfo> defs = new();
			var op = _master.AddSynchronousOp( () =>
			{
				defs = FileDefListing.List( _master, null, id, q["machine"], q["app"] );
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;

			if( defs.Count == 0 )
				throw HttpException.NotFound( $"No file definition matches '{id}'." );

			await SendJson( FileDefListing.ToJson( defs ) );
		}

		// body: {"fileDef":{"id":"logs","machine":"m1","app":"a"}} or {"app":"m1.a@plan"}, optional "comment"
		[Route( HttpVerbs.Post, "/downloads" )]
		public async Task StartDownload()
		{
			var body = await HttpContext.GetRequestBodyAsStringAsync();

			DownloadRequest? request;
			try { request = DownloadJobs.ParseRequest( body ); }
			catch( Exception ) { throw HttpException.BadRequest( "The body is not a valid download request." ); }

			if( request is null ) throw HttpException.BadRequest();

			Guid id = Guid.Empty;
			var op = _master.AddSynchronousOp( () => { id = _master.DownloadJobs.Start( request ); } );
			await op.WaitAsync();

			if( op.Exception is AppFilesNotFoundException ) throw HttpException.NotFound( op.Exception.Message );
			if( op.Exception is ArgumentException or ArgumentSyntaxErrorException ) throw HttpException.BadRequest( op.Exception.Message );
			if( op.Exception != null ) throw op.Exception;

			HttpContext.Response.StatusCode = (int) HttpStatusCode.Accepted;
			await SendJson( AppFilesResult.ToJson( new { id = id.ToString() } ) );
		}

		// The preview of a selection: {"fileDefs":[{"id":"pkg.a","machine":"m1"}],"apps":["m1.a"]}; changes nothing.
		// ?content=false skips scanning the folders.
		[Route( HttpVerbs.Post, "/files/query" )]
		public async Task QueryFiles()
		{
			var body = await HttpContext.GetRequestBodyAsStringAsync();

			FileSelectionRequest? request;
			try { request = FileSelection.ParseJson( body ); }
			catch( Exception ) { throw HttpException.BadRequest( "The body is not a valid selection." ); }
			if( request is null ) throw HttpException.BadRequest( "The body is not a valid selection." );

			var contentArg = HttpContext.GetRequestQueryData()["content"];
			var includeContent = !string.Equals( contentArg, "false", StringComparison.OrdinalIgnoreCase ) && contentArg != "0";

			// the items are looked up on the master thread, resolving them (which waits for the agents) is not
			FileSelection? selection = null;
			var op = _master.AddSynchronousOp( () => { selection = FileSelection.Parse( _master, request ); } );
			await op.WaitAsync();

			if( op.Exception is AppFilesNotFoundException ) throw HttpException.NotFound( op.Exception.Message );
			if( op.Exception is ArgumentException or ArgumentSyntaxErrorException ) throw HttpException.BadRequest( op.Exception.Message );
			if( op.Exception != null ) throw op.Exception;

			var result = await selection!.ResolveAsync( includeContent );
			await SendJson( result.ToJson() );
		}

		[Route( HttpVerbs.Get, "/downloads" )]
		public async Task ListDownloads()
		{
			await SendJson( AppFilesResult.ToJson( _master.DownloadJobs.List() ) );
		}

		// ?wait=N holds the answer until the job has ended, at most N seconds (max 120)
		[Route( HttpVerbs.Get, "/downloads/{id}" )]
		public async Task GetDownload( string id )
		{
			var guid = ParseId( id );

			var waitArg = HttpContext.GetRequestQueryData()["wait"];
			double waitSec = 0;
			if( !string.IsNullOrEmpty( waitArg ) )
				double.TryParse( waitArg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out waitSec );
			waitSec = Math.Min( Math.Max( waitSec, 0 ), 120 );

			var until = DateTime.UtcNow.AddSeconds( waitSec );
			while( _master.DownloadJobs.IsJobRunning( guid ) && DateTime.UtcNow < until )
				await Task.Delay( 200 );

			var view = _master.DownloadJobs.Get( guid ) ?? throw HttpException.NotFound( $"No download {guid}." );
			await SendJson( view.ToJson() );
		}

		[Route( HttpVerbs.Get, "/downloads/{id}/archives/{name}" )]
		public async Task GetDownloadFile( string id, string name )
		{
			string path;
			try
			{
				path = _master.DownloadJobs.FileToServe( ParseId( id ), name );
			}
			catch( DownloadJobException e )
			{
				throw e.Problem switch
				{
					EDownloadJobProblem.Forbidden => HttpException.Forbidden( e.Message ),
					EDownloadJobProblem.Conflict => new HttpException( HttpStatusCode.Conflict, e.Message ),
					_ => HttpException.NotFound( e.Message ),
				};
			}

			// streamed from the disk, never held in memory: an archive may be many gigabytes
			using var source = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true );

			HttpContext.Response.ContentType = "application/zip";
			HttpContext.Response.Headers.Add( "Content-Disposition", ContentDisposition( name ) );
			HttpContext.Response.ContentLength64 = source.Length;

			using var output = HttpContext.OpenResponseStream( buffered: false, preferCompression: false );
			await source.CopyToAsync( output );
		}

		// stops the job if it runs and removes it
		[Route( HttpVerbs.Delete, "/downloads/{id}" )]
		public async Task DeleteDownload( string id )
		{
			var guid = ParseId( id );

			var op = _master.AddSynchronousOp( () => _master.DownloadJobs.Delete( guid ) );
			await op.WaitAsync();

			if( op.Exception is DownloadJobException ) throw HttpException.NotFound( op.Exception.Message );
			if( op.Exception != null ) throw op.Exception;

			HttpContext.Response.StatusCode = (int) HttpStatusCode.NoContent;
		}

		/// <summary>An ASCII fallback for old clients plus the RFC 5987 form, which carries any name.</summary>
		static string ContentDisposition( string name )
		{
			var fallback = new string( name.Select( c => c < 32 || c > 126 || c == '"' || c == (char) 92 ? '_' : c ).ToArray() );
			return $"attachment; filename=\"{fallback}\"; filename*=UTF-8''{Uri.EscapeDataString( name )}";
		}

		static Guid ParseId( string id )
			=> Guid.TryParse( id, out var guid ) ? guid : throw HttpException.BadRequest( $"'{id}' is not a download id." ); // malformed, like a script id that is no guid

		Task SendJson( string json )
			=> HttpContext.SendStringAsync( json, "application/json", ApiOps.Utf8 );
	}
}
