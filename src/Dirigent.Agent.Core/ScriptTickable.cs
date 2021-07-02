using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dirigent.Net;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent
{
	public class ScriptCtrl
	{
		private Master _master;
		
		public ScriptCtrl( Master master )
		{
			_master = master;
		}

		// apps

		public void StartApp( string id, string? planName, string? vars=null )
		{
			_master.Send( new Net.StartAppMessage( string.Empty, new AppIdTuple(id), planName, flags:0, vars:Tools.ParseEnvVarList(vars) ) );
		}

		public void RestartApp( string id, string? vars=null )
		{
			_master.Send( new Net.RestartAppMessage( string.Empty, new AppIdTuple(id), vars:Tools.ParseEnvVarList(vars) ) );
		}

		public void KillApp( string id )
		{
			_master.Send( new Net.KillAppMessage( string.Empty, new AppIdTuple(id) ) );
		}

		public AppState? GetAppState( string id )
		{
			return _master.GetAppState( new AppIdTuple( id ) );
		}

		// plans

		public void StartPlan( string id )
		{
			_master.Send( new Net.StartPlanMessage( string.Empty, id ) );
		}

		public void RestartPlan( string id )
		{
			_master.Send( new Net.RestartPlanMessage( string.Empty, id ) );
		}

		public void KillPlan( string id )
		{
			_master.Send( new Net.KillPlanMessage( string.Empty, id ) );
		}

		public PlanState? GetPlanState( string id )
		{
			return _master.GetPlanState( id );
		}

		// clients

		public ClientState? GetClientState( string id )
		{
			return _master.GetClientState( id );
		}

		// scritps

		public void StartScript( string idWithArgs )
		{
			(var id, var args) = Tools.ParseScriptIdArgs( idWithArgs );

			_master.Send( new StartScriptMessage( string.Empty, id, args ) );
		}

		public void KillScript( string id )
		{
			_master.Send( new KillScriptMessage( string.Empty, id ) );
		}

	}

}
