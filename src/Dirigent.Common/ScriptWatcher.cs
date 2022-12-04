using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Monitors a running script, gets notified when its status changes.
	/// Periodically ticked from main thread.
	/// </summary>
	public class ScriptStatusWatcher : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		protected string _localClientId;

		Action<ScriptState> _onStatusChanged;

		public bool ShallBeRemoved { get; set; }


		public ScriptStatusWatcher( string localClientId, Action<ScriptState> onStatusChanged )
		{
			_localClientId = localClientId;
			_onStatusChanged = onStatusChanged;
		}

		/// <summary>
		/// Called when the task has finished. The task has already been removed from the system.
		/// </summary>
		public virtual void OnStatusChanged( ScriptState state )
		{
			_onStatusChanged( state );
		
			// be default just quit if task has died
			if ( !state.IsAlive )
			{
				ShallBeRemoved = true;
			}
		}

		public virtual void Tick()
		{
		}
	}

	
}

