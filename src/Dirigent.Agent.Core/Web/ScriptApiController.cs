using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using System.Linq;

namespace Dirigent.Web
{
    public class ScriptStateDetails
    {
        public string code { get; set; } = string.Empty;
		//public string flags { get; set; } = string.Empty;

		public ScriptStateDetails()
		{
		}

		public ScriptStateDetails( Dirigent.ScriptState ss )
		{
			code = Tools.GetScriptStateText( ss );
			//flags = "";
		}
    }

    public class ScriptState
    {
        public string id { get; set; } = string.Empty;
        public ScriptStateDetails state { get; set; } = new();

		public ScriptState()
		{
		}

		public ScriptState( string id, Dirigent.ScriptState ps )
		{
			this.id = id;
			this.state = new ScriptStateDetails( ps );
		}
    }

    public class ScriptDef
    {
        public string id { get; set; } = string.Empty;

		public string groups { get; set; } = string.Empty;

		public ScriptDef()
		{
		}

		public ScriptDef( Dirigent.ScriptDef sd )
		{
			id = sd.Id.ToString();
			groups = sd.Groups;
		}

    }

    public class ScriptApiController : WebApiController
    {
		private Master _master;

		public ScriptApiController( Master master )
		{
			_master = master;
		}

		// Gets all scripts defs.
		[Route( HttpVerbs.Get, "/scriptdefs" )]
		public async Task<IEnumerable<ScriptDef>> GetAllScriptsDefs()
		{
			List<ScriptDef> res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				res = (from pd in _master.GetAllScriptDefs() select new ScriptDef( pd )).ToList();
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

		// Gets single script def.
		[Route( HttpVerbs.Get, "/scriptdefs/{id}" )]
		public async Task<ScriptDef> GetScriptDef( string id )
		{
			ScriptDef res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				var pd = _master.GetScriptDef( Guid.Parse(id) );

				if( pd is null )
					throw HttpException.NotFound();

				res = new ScriptDef( pd );
			} );
			await op.WaitAsync();
			return res;
		}

		// Gets one concrete script state.
		[Route( HttpVerbs.Get, "/scriptstate/{id}" )]
		public async Task<ScriptState> GetScriptState( string id )
		{
			ScriptState res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				var ps = _master.GetScriptState( Guid.Parse(id) );

				if( ps is null )
					throw HttpException.NotFound();

				res = new ScriptState( id, ps );
			} );
			await op.WaitAsync();
			return res;
		}

		// Gets all scripts states.
		[Route( HttpVerbs.Get, "/scriptstates" )]
		public async Task<IEnumerable<ScriptState>> GetAllScriptsStates()
		{
			List<ScriptState> res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				res = (from kv in _master.GetAllScriptStates() select new ScriptState( kv.Key.ToString(), kv.Value )).ToList();
			} );
			await op.WaitAsync();
			return res;
		}

	}
}
