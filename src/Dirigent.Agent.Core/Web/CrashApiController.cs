using System;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

using Dirigent.Crashes;

namespace Dirigent.Web
{
	/// <summary>The crashes of the programs of the site - see <see cref="CrashQuery"/>.</summary>
	public class CrashApiController : WebApiController
	{
		readonly Master _master;

		public CrashApiController( Master master )
		{
			_master = master;
		}

		// ?since=&until=&component=&app=&machine=&kind=&id=&newestPerApp=&details=1 - lists repeated or comma-separated
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/crashes" )]
		public async Task GetCrashes()
		{
			var q = HttpContext.GetRequestQueryData();

			CrashQuery? query = null;
			var op = _master.AddSynchronousOp( () =>
			{
				try
				{
					query = CrashQuery.Prepare( _master, CrashFilterParser.Parse( q ), CrashFilterParser.IsTrue( q["details"] ), CrashFilterParser.ParseTimeout( q["timeout"] ) );
				}
				catch( ArgumentException e )
				{
					throw HttpException.BadRequest( e.Message );
				}
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;

			var listing = await query!.RunAsync();
			await HttpContext.SendStringAsync( AppFilesResult.ToJson( listing ), "application/json", ApiOps.Utf8 );
		}
	}
}
