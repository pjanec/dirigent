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
		[Route( HttpVerbs.Post, "/cli" )]
		public async Task<string> PostCliCmd()
		{
			using var textReader = HttpContext.OpenRequestText();
			var line = textReader.ReadLine();
			if( line is not null )
			{
				var cliClient = new CLIClient();
				// FIXME! here we need to await the finishing of the request, not just adding the request to a queue!!!
				//        Otherwise the response will be empty as the request will not be processed yet
				//		  CLIProcessor should support async waiting for request completion (via awaiting the mutex  the request?)
				var r = _master.AddCliRequest( cliClient, line );
				await r.WaitAsync();
				return cliClient.Response; 
			}
			return string.Empty;
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
