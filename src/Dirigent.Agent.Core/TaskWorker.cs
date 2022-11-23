using System;
using System.Collections.Generic;

namespace Dirigent
{
	/// <summary>
	/// Represetns an instance of an active distributed task in the system.
	/// This is its part running on the agent (the worker).
	/// </summary>
	/// <remarks>
	/// Worker gets instantiated based on request from the controller.
	/// </remarks>
	public class DTaskWorker: Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Guid Guid { get; private set; }

		public string Id = String.Empty; // for debug display purposes
		
		public Guid TaskInstance { get; private set; }

		public DTaskState State = new();

		private Script? _script; // worker part


		private Agent _agent;
					
		public DTaskWorker( Agent agent, Guid taskInstance, string taskId, string? args )
		{
			Guid = Guid.NewGuid();
			_agent = agent;
			TaskInstance = taskInstance;
			Id = taskId;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// dispose managed resources
			_script?.Dispose();
		}

		
		public void Start( string scriptCode, string? args, string? scriptOrigin )
		{
			var script = ScriptFactory.CreateFromString( TaskInstance, Id, scriptCode, args, _agent, scriptOrigin	 );
			Start( script, args );
		}
		

		/// <summary>
		/// Starts the controller part
		/// </summary>
		/// <param name="script">Freshly created, no yet Init-ed script</param>
		public void Start( Script script, string? args )
		{
			_script = script;

			_script.Init();
		}

		// Kills the worker script
		public void Kill()
		{
			if( _script is null ) // not running anumore?
				return;

			Remove();			
		}

		void Remove()
		{
			if( _script is null ) return;
			
			_script.Dispose();
			
			_script = null;
		}

		public void Tick()
		{
			if( _script != null )
			{
				_script.Tick();

				if( _script.ShallBeRemoved )
				{
					Remove();
				}
			}

			State.StatusText = _script != null ? _script.StatusText : "None";
		}
	}
}

