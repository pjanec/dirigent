using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dirigent
{
	/// <summary>
	/// Interface used for dynamic instantiation of scripts
	/// </summary>
	public interface IScript : IDisposable
	{
		//string StatusText { get; set; }
		//ScriptCtrl Ctrl { get; set; }
		//string Args { get; set; }
		//void Init();
		//void Done();
		void Tick();
		bool ShallBeRemoved { get; set; }
	}

	/// <summary>
	/// Script instantiated dynamically from a C# source file
	/// </summary>
	public class Script : Disposable, IScript
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// What task this script belongs to
		/// </summary>
		public Guid TaskInstance { get; set; }

		public bool ShallBeRemoved { get; set; }

		public string StatusText { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public string Args { get; set; } = string.Empty;

		// initialized during installation
		#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public IDirig Ctrl { get; set; }
		#pragma warning restore CS8618


		Coroutine? _run;
		Coroutine? _onRequest;

		List<Net.TaskRequestMessage> _requests = new List<Net.TaskRequestMessage>();

		bool tickCoroutine( ref Coroutine? coro )
		{
			if( coro != null )
			{
				coro.Tick();
				if( !coro.IsFinished )
				{
					return true;
				}
				coro.Dispose();
				coro = null;
			}
			return false;
		}


		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;

			Done();
			
			_run?.Dispose(); _run = null;
			_onRequest?.Dispose(); _onRequest = null;
		}

		public void Tick()
		{
			if( _run == null )
			{
				_run = new Coroutine( Run() );
			}

			if( !tickCoroutine( ref _run ) )
				ShallBeRemoved = true;

			// process next waiting request if the previous is finished
			if( _onRequest == null )
			{
				if( _requests.Count > 0 )
				{
					var req = _requests.First();
					_requests.Remove( req );

					_onRequest = new Coroutine( OnRequest( req.RequestId, req.Type, req.Args ) );
				}
			}

			tickCoroutine( ref _onRequest );
		}

		public void AddRequest( Net.TaskRequestMessage req )
		{
			_requests.Add( req );
		}

		/// <summary> called once when script gets instantiated, before first tick </summary>
		public virtual void Init()
		{
		}

		/// <summary> called once when script gets destroyed </summary>
		public virtual void Done()
		{
		}

		public virtual System.Collections.IEnumerable Run()
		{
			yield return null;
		}


		public virtual System.Collections.IEnumerable OnRequest( Guid id, string type, string? args )
		{
			yield return null;
		}

		public void SendRequest( string type, string? args )
		{
			Ctrl.Send( new Net.TaskRequestMessage( TaskInstance, type, args ) );
		}

		public void SendResponse( Guid requestId, string type, string? args )
		{
			Ctrl.Send( new Net.TaskResponseMessage( TaskInstance, requestId, type, args ) );
		}

		public void StartApp( string id, string? planName, string? vars=null )
		{
			Ctrl.Send( new Net.StartAppMessage( string.Empty, new AppIdTuple(id), planName, flags:0, vars:Tools.ParseEnvVarList(vars) ) );
		}

		public void RestartApp( string id, string? vars=null )
		{
			Ctrl.Send( new Net.RestartAppMessage( string.Empty, new AppIdTuple(id), vars:Tools.ParseEnvVarList(vars) ) );
		}

		public void KillApp( string id )
		{
			Ctrl.Send( new Net.KillAppMessage( string.Empty, new AppIdTuple(id) ) );
		}

		public AppState? GetAppState( string id )
		{
			return Ctrl.GetAppState( new AppIdTuple( id ) );
		}

		// plans

		public void StartPlan( string id, string? vars=null )
		{
			Ctrl.Send( new Net.StartPlanMessage( string.Empty, id, vars:Tools.ParseEnvVarList(vars) ) );
		}

		public void RestartPlan( string id, string? vars=null )
		{
			Ctrl.Send( new Net.RestartPlanMessage( string.Empty, id, vars:Tools.ParseEnvVarList(vars) ) );
		}

		public void KillPlan( string id )
		{
			Ctrl.Send( new Net.KillPlanMessage( string.Empty, id ) );
		}

		public PlanState? GetPlanState( string id )
		{
			return Ctrl.GetPlanState( id );
		}

		// clients

		public ClientState? GetClientState( string id )
		{
			return Ctrl.GetClientState( id );
		}

		// scritps

		public void StartScript( string idWithArgs )
		{
			(var id, var args) = Tools.ParseScriptIdArgs( idWithArgs );

			Ctrl.Send( new Net.StartScriptMessage( string.Empty, id, args ) );
		}

		public void KillScript( string id )
		{
			Ctrl.Send( new Net.KillScriptMessage( string.Empty, id ) );
		}

	}

}
