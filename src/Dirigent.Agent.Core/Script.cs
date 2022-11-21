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
	public interface IScript : ITickable
	{
		//string StatusText { get; set; }
		//ScriptCtrl Ctrl { get; set; }
		//string Args { get; set; }
		//void Init();
		//void Done();
	}

	/// <summary>
	/// Script instantiated dynamically from a C# source file
	/// </summary>
	public class Script : Disposable, IScript
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		
		public string Id { get; set; } = string.Empty;

		public bool ShallBeRemoved { get; protected set; }

		public uint Flags => 0;

		public string StatusText { get; set; } = string.Empty;

		public string FileName { get; set; } = string.Empty;

		public string Args { get; set; } = string.Empty;

		public Action? OnRemoved { get; set; }

		// initialized during installation
		#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public IDirig Ctrl { get; set; }
		#pragma warning restore CS8618


		protected Coroutine? Coroutine;

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;

			Done();
			
			if( Coroutine != null )
			{
				Coroutine.Dispose();
				Coroutine = null;
			}
		}

		/// <summary> called once when script gets instantiated </summary>
		public virtual void Init()
		{
		}

		/// <summary> called once when script gets destroyed </summary>
		public virtual void Done()
		{
		}

		/// <summary> called every frame </summary>
		public virtual void Tick()
		{
		}

		void ITickable.Tick()
		{
			// tick coroutine if exists; remove script when coroutine finishes
			if( Coroutine != null )
			{
				Coroutine.Tick();
				if( Coroutine.IsFinished )
				{
					Coroutine.Dispose();
					Coroutine = null;
					
					ShallBeRemoved = true;
				}
			}

			// call the virtual method
			Tick();
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
