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
		// GET api/cmd/StartApp?id=m1.a&plan=plan1
		[Route( HttpVerbs.Get, "/cmd/{name}" )]
		public string ExecCmd( string name, [QueryData] NameValueCollection parameters )
		{
			var sb = new StringBuilder();
			sb.Append( $"cmd {name}\n" );
			foreach (var key in parameters.AllKeys)
			{
				var value = parameters[key];
				sb.Append( $"  par {key} = {value}\n" );
			}
			var response = sb.ToString();
			response.Info();
			return response;
		}
	}
}
