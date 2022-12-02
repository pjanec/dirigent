
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;
using System.Text.RegularExpressions;

namespace Dirigent
{

	/// <summary>
	/// Starts scripts locally.
	/// Maintains running instances of local scripts, provides their state and allows to kill them.
	/// Removes the instance shortly after it dies.
	/// </summary>
	public class LocalScriptRegistry : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		string _clientId => _ctrl.Name;

		IDirig _ctrl;
		ScriptFactory _scriptFactory;
		SynchronousOpProcessor _syncOps;
		
		// all currently running scripts on this client
		Dictionary<Guid, LocalScript> _scripts = new Dictionary<Guid, LocalScript>();
		public Dictionary<Guid, LocalScript> Scripts => _scripts;
		public Dictionary<Guid, ScriptState> ScriptStates => Scripts.Values.ToDictionary( p => p.Instance, p => p.State );

		public LocalScriptRegistry( IDirig ctrl, ScriptFactory factory, SynchronousOpProcessor syncOps )
		{
			_ctrl = ctrl;
			_scriptFactory = factory;
			_syncOps = syncOps;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// dispose managed resources
			foreach (var t in _scripts.Values)
			{
				t.Dispose();
			}
		}

		public void Tick()
		{
			var toRemove = new List<LocalScript>(10);
			foreach( var p in _scripts.Values )
			{
				p.Tick();

				// houskeeping
				// forget those already dead
				const double ForgettableTaskTimeout = 10.0;
				var now = DateTime.Now;
				if( !p.State.IsAlive )
				{
					if( (now - p.LastAliveTime).TotalSeconds > ForgettableTaskTimeout )
					{
						toRemove.Add(p);
					}
				}
			}

			foreach( var taskInfo in toRemove )
			{
				Remove( taskInfo );
			}
		}

		void Remove( LocalScript entry )
		{
			entry.Dispose();
			_scripts.Remove( entry.Instance );
		}

		public void Start( Guid instance, string scriptName, byte[]? args, string title )
		{
			if( _scripts.TryGetValue( instance, out var entry ) )
			{
				if( entry.State.IsAlive )
				{
					log.Warn( $"Script {title} [{instance}] already running on {_clientId}. Ignoring start request." );
					return;
				}
				else
				{
					Remove( entry );
				}
			}

			entry = new LocalScript( _ctrl, _scriptFactory, _syncOps, instance );
			_scripts.Add( instance, entry );

			entry.Start( scriptName, args, title );
		}

		public void Stop( Guid instance )
		{
			if (!_scripts.TryGetValue( instance, out var entry ))
			{
				log.Warn( $"Script [{instance}] not running on {_clientId}. Ignoring stop request." );
				return;
			}

			entry.Stop();
		}


		///// <summary>
		///// Starts given task and install its watcher. If the task fails to start, it gets removed automatically.
		///// </summary>
		//public Guid StartTaskWithWatcher( string scriptName, byte[]? args, string title, ScriptWatcher watcher )
		//{
		//	var taskInstance = Guid.NewGuid();

		//	// send a request
		//	_ctrl.Send( new Net.StartTaskMessage( _ctrl.Name, taskInstance, scriptName, args, title ) );

		//	// add task record with "Starting" status; it will get removed if fails to start in some time
		//	var taskInfo = new LocalScript { Guid=taskInstance };
		//	taskInfo.State.Status = EScriptStatus.Starting;
		//	taskInfo.Watchers.Add( watcher );
		//	LocalScripts[taskInstance] = taskInfo;

		//	return taskInstance;

		//}


		/// <summary>
		/// Created when a scrpt instance is to be started.
		/// Removed few secs after the script instance dies.
		/// </summary>
		public class LocalScript : Disposable
		{
			public Guid Instance;
			public ScriptRunner Runner;
			public ScriptState State => Runner.State;

			public DateTime LastAliveTime = DateTime.Now;

			public LocalScript( IDirig ctrl, ScriptFactory factory, SynchronousOpProcessor syncOps, Guid instance )
			{
				Instance = instance;
				Runner = new ScriptRunner( ctrl, instance, factory, syncOps );
			}

			protected override void Dispose( bool disposing )
			{
				base.Dispose( disposing );
				if (!disposing) return;

				// dispose managed resources
				Runner.Dispose();
			}

			public void Start( string scriptName, byte[]? args, string title )
			{
				Runner.Start( scriptName, args, title );
			}

			public void Stop()
			{
				Runner.Stop();
			}

			public void Tick()
			{
				Runner.Tick();

				if( Runner.State.IsAlive )
				{
					LastAliveTime = DateTime.Now;
				}
			}
		}

	}
}

