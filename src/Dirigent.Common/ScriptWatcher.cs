using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Monitors a running script, gets notified when it finished.
	/// Periodically ticked from main thread.
	/// </summary>
	public class ScriptWatcher : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		protected string _localClientId;

		public bool ShallBeRemoved { get; set; }


		public ScriptWatcher( string localClientId )
		{
			_localClientId = localClientId;
		}

		/// <summary>
		/// Called when the task has finished. The task has already been removed from the system.
		/// </summary>
		public virtual void OnStatusChanged( ScriptState state )
		{
			// be default just quit if task has died
			if( !state.IsAlive )
			{
				ShallBeRemoved = true;
			}
		}

		public virtual void Tick()
		{
		}
	}

	/// <summary>
	/// Calls given action when task finishes
	/// </summary>
	public class ScriptFinishedWatcher : ScriptWatcher
	{
		Action _onFinished;

		public ScriptFinishedWatcher( string localClientId, Action onFinished )
			: base( localClientId )
		{
			_onFinished = onFinished;
		}

		public override void OnStatusChanged( ScriptState state )
		{
			base.OnStatusChanged( state );

			if( state.Status == EScriptStatus.Finished )
			{
				_onFinished?.Invoke();
			}
		}
	}
	
}

