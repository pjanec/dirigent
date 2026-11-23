using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using Swan.Logging;
using System.Collections.Specialized;

namespace Dirigent.Web
{
    public class CmdApiController : WebApiController
    {
		private Master _master;

		public CmdApiController( Master master )
		{
			_master = master;
		}

		// Created for each command
		// Caches the response into internal buffer
		private class CLIClient : Dirigent.ICLIClient
		{
			StringBuilder _sb = new StringBuilder();

			public string Name => "WebApi";
			public string Response => _sb.ToString();

			public void WriteResponse(string text)
			{
				_sb.Append( text );
			}
		}


		// POST api/cli   data: "StartApp m1.a@plan1"
		[Scope( ScopeAttribute.Control )]
		[Route( HttpVerbs.Post, "/cli" )]
		public async Task PostCliCmd()
		{
			using var textReader = HttpContext.OpenRequestText();
			var line = textReader.ReadLine();
			await textReader.ReadToEndAsync(); // the rest is not used, but has to be read - see ApiOps.DiscardBody

			// nothing to run is a request not understood, not a command that answered nothing
			if( string.IsNullOrWhiteSpace( line ) )
				throw HttpException.BadRequest( "A command is needed: one line of CLI text, e.g. GetAllPlansState." );

			var cliClient = new CLIClient();
			// FIXME! here we need to await the finishing of the request, not just adding the request to a queue!!!
			//        Otherwise the response will be empty as the request will not be processed yet
			//		  CLIProcessor should support async waiting for request completion (via awaiting the mutex  the request?)
			var r = _master.AddCliRequest( cliClient, line );
			await r.WaitAsync();

			// One JSON string, its line breaks escaped. Returned as a string, it went through EmbedIO's serializer, which
			// left them raw - and every CLI answer ends with one, so no answer was valid JSON.
			await HttpContext.SendStringAsync( System.Text.Json.JsonSerializer.Serialize( cliClient.Response ), "application/json", ApiOps.Utf8 );
		}

		//// GET api/cmd/StartApp?id=m1.a&plan=plan1
		//[Route( HttpVerbs.Get, "/cmd/{name}" )]
		//public string ExecCmd( string name, [QueryData] NameValueCollection parameters )
		//{
		//	lock( _master )
		//	{
		//		var sb = new StringBuilder();
		//		sb.Append( $"cmd {name}\n" );
		//		foreach (var key in parameters.AllKeys)
		//		{
		//			var value = parameters[key];
		//			sb.Append( $"  par {key} = {value}\n" );
		//		}
		//		var response = sb.ToString();
		//		response.Info();
		//		return response;
		//	}
		//}
	}
}
