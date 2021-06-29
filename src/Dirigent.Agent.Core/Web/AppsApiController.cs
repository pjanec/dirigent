using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;

namespace Dirigent.Web
{
    public class AppDef
    {
        public string Id { get; set; } = string.Empty;
        public string ExeFullPath { get; set; } = string.Empty;
        public string CmdLineArgs { get; set; } = string.Empty;
    }

    public class AppsApiController : WebApiController
    {
		// Gets all plans defs.
		[Route( HttpVerbs.Get, "/app/defs" )]
		public IEnumerable<AppDef> GetAllAppDefs()
		{
			return new List<AppDef>
			{
				new AppDef()
				{
					Id = "m1.a",
					ExeFullPath = "notepad.exe",
					CmdLineArgs = "C:\\",
				},

				new AppDef()
				{
					Id = "m1.b",
					ExeFullPath = "notepad.exe",
					CmdLineArgs = "C:\\",
				},
			};
		}

		// Gets single plan def.
		[Route( HttpVerbs.Get, "/app/defs/{id}" )]
		public IEnumerable<AppDef> GetAppDef( string id )
		{
			return new List<AppDef>
			{
				new AppDef
				{
					Id = id,
					ExeFullPath = "notepad.exe",
					CmdLineArgs = "C:\\",
				},
			};
		}
	}
}
