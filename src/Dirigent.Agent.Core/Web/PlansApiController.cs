using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;

namespace Dirigent.Web
{
    public class PlanStateDetails
    {
        public string code { get; set; } = string.Empty;
    }

    public class PlanState
    {
        public string id { get; set; } = string.Empty;
        public PlanStateDetails state { get; set; } = new();
    }

    public class PlanDef
    {
        public string Name { get; set; } = string.Empty;

        public List<AppDef> AppDefs  { get; set; } = new();

        public Dictionary<string, object> Whatever
        {
            get
            {
                return new Dictionary<string,object>
                {
                    { "ahoj", 23 },
                };
            }
            set
            {
            }
        }
    }

    public class PlansApiController : WebApiController
    {
		// Gets all plans defs.
		[Route( HttpVerbs.Get, "/plan/defs" )]
		public IEnumerable<PlanDef> GetAllPlansDefs()
		{
			return new List<PlanDef>
			{
				new PlanDef
				{
					Name = "plan1",

					AppDefs = new List<AppDef>
					{
						new AppDef()
						{
							Id = "m1.a1",
							ExeFullPath = "notepad.exe",
							CmdLineArgs = "C:\\",
						},
					},
				},
			};
		}

		// Gets single plan def.
		[Route( HttpVerbs.Get, "/plan/defs/{id}" )]
		public IEnumerable<PlanDef> GetPlanDef( string id )
		{
			return new List<PlanDef>
			{
				new PlanDef
				{
					Name = id,

					AppDefs = new List<AppDef>
					{
						new AppDef()
						{
							Id = "m1.a1",
							ExeFullPath = "notepad.exe",
							CmdLineArgs = "C:\\",
						},
					},
				},
			};
		}

		// Gets all plans states.
		[Route( HttpVerbs.Get, "/plan/states" )]
		public IEnumerable<PlanState> GetAllPlansStates()
		{

			return new List<PlanState>
			{
				new PlanState
				{
					id = "plan1",
					state = new PlanStateDetails
					{
						code = "InProgress"
					}
				},

				new PlanState
				{
					id = "plan2",
					state = new PlanStateDetails
					{
						code = "None"
					}
				}

			};
		}

	}
}
