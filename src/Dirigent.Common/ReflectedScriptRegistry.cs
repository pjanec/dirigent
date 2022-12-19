
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace Dirigent
{

	/// <summary>
	/// Monitors the status of all running scripts on all nodes.
	/// If the script finishes, it is kept in the registry for a while, so that the client can query its result.
	/// Allows starting new scripts.
	/// Allows to run a new script and to attach a local watcher to it.
	/// </summary>
	public class ReflectedScriptRegistry : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		string _clientId => _ctrl.Name;

		IDirig _ctrl;
		
		// all currently running scripts anywhere in the system
		Dictionary<Guid, ReflectedScript> _scripts = new Dictionary<Guid, ReflectedScript>();

		public ScriptState? GetScriptState( Guid id ) { if( _scripts.TryGetValue(id, out var rs)) return rs.State; else return null; }
		public IEnumerable<KeyValuePair<Guid, ScriptState>> GetAllScriptStates() => _scripts.Select( p => new KeyValuePair<Guid, ScriptState>( p.Key, p.Value.State ) );

		// script definitions (from shared config)
		public List<ScriptDef> _scriptDefs = new List<ScriptDef>();
		public List<ScriptDef> ScriptDefs => _scriptDefs;
																					

		public ReflectedScriptRegistry( IDirig ctrl )
		{
			_ctrl = ctrl;
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

		public void Clear()
		{
			_scriptDefs.Clear();
			_scripts.Clear();
		}

		public void SetScriptDefs( IEnumerable<ScriptDef>? scriptDefs, bool incremental=false )
		{
			if( incremental ) // replace
			{
				if( scriptDefs is not null )
					scriptDefs = new List<ScriptDef>( scriptDefs );
				else
					_scriptDefs = new List<ScriptDef>();
			}
			else // add/update
			{
				if( scriptDefs is not null )
				{
					foreach( var pd in scriptDefs )
					{
						int idx = _scriptDefs.FindIndex( (x) => x.Id == pd.Id );
						if( idx < 0 )
						{
							_scriptDefs.Add( pd );
						}
						else
						{
							_scriptDefs[idx] = pd;
						}
					}
				}
			}
		}


		public void Tick()
		{
			var toRemove = new List<ReflectedScript>(10);
			foreach( var p in _scripts.Values )
			{
				p.Tick();

				// houskeeping
				// forget those already dead or starting for too long time
				const double ForgettableTimeout = 10.0;
				var now = DateTime.Now;
				if( p.State.Status != EScriptStatus.Running )
				{
					if( (now - p.LastAliveTime).TotalSeconds > ForgettableTimeout )
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

		void Remove( ReflectedScript taskInfo )
		{
			taskInfo.Dispose();
			_scripts.Remove( taskInfo.Guid );
		}

		public void AttachWatcher( Guid scriptInstance, ScriptStatusWatcher watcher )
		{
			if (!_scripts.TryGetValue( scriptInstance, out var entry ))
			{
				// If task not running yet, remember it as "Starting" and set a timeout to forget about it
				// if we never get an update from its controller

				throw new Exception( $"Script {scriptInstance} not found." );
			}
			entry.Watchers.Add( watcher );
		}

		public void DetachWatcher( Guid scriptInstance, ScriptStatusWatcher watcher )
		{
			if( !_scripts.TryGetValue( scriptInstance, out var taskInfo ))
			{
				throw new Exception( $"Script {scriptInstance} not found." );
			}
			// will detach next tick
			watcher.ShallBeRemoved = true;
		}

		public void UpdateScriptState( Guid scriptInstance, ScriptState state )
		{
			// create task record if we don't know about it yet
			if (!_scripts.TryGetValue( scriptInstance, out var entry ))
			{
				if( !state.IsAlive ) // no need to remember an already dead task
					return;

				entry = new ReflectedScript { Guid=scriptInstance };
				_scripts[scriptInstance] = entry;
			}

			entry.OnScriptState( state );
		}

		/// <summary>
		/// Starts script on given client and install its watcher. If the script fails to start, watcher gets removed automatically.
		/// </summary>
		public Guid StartScriptWithWatcher( string clientId, string scriptName, string? sourceCode, byte[]? args, string title, ScriptStatusWatcher watcher )
		{
			var instance = Guid.NewGuid();

			// send a request
			_ctrl.Send( new Net.StartScriptMessage( _ctrl.Name, instance, scriptName, sourceCode, args, title, clientId ) );

			// add task record with "Starting" status; it will get removed if fails to start in some time
			var entry = new ReflectedScript { Guid = instance };
			entry.State.Status = EScriptStatus.Starting;
			entry.Watchers.Add( watcher );
			_scripts[instance] = entry;

			return instance;
		}

		public void RunScriptNoWait<TArgs>( string clientId, string scriptName, string? sourceCode, TArgs? args, string title )
		{
			var instance = Guid.NewGuid();
			var argsBytes = args is null ? null : Tools.Serialize( args );

			// send a request
			_ctrl.Send( new Net.StartScriptMessage( _ctrl.Name, instance, scriptName, sourceCode, argsBytes, title, clientId ) );
		}

		// runs script on given machine and wait for its termination
		// throws ScriptException on failure
		// throws TimeoutException on timeout
		// otherwise returns the deserialized value that was serialized by the script
		public async Task<TResult?> RunScriptAndWaitAsync<TArgs,TResult>( string clientId, string scriptName, string? sourceCode, TArgs? args, string title, CancellationToken ct, int timeoutMs=-1 )
		{
			var tcs = new TaskCompletionSource<TResult?>();
			
			var watcher = new ScriptStatusWatcher( _clientId, ( state ) =>
			{
				if( state.Status == EScriptStatus.Failed )
				{
					// we throw a task failure exception to the task
					var exception = Tools.Deserialize<ScriptException>(state.Data)!;
					tcs.SetException( exception );
				}
				else if( state.Status == EScriptStatus.Cancelled )
				{
					// this throws classic TaskCancelledException
					tcs.SetCanceled();
				}
				else if (state.Status == EScriptStatus.Finished)
				{
					var result = Tools.Deserialize<TResult>(state.Data);
					tcs.SetResult( result );
				}
			} );
			
			StartScriptWithWatcher( clientId, scriptName, sourceCode, Tools.Serialize(args), title, watcher );
			
			if( timeoutMs < 0 )
			{
				return await tcs.Task.WaitAsync( ct );
			}
			else
			{
				return await tcs.Task.WaitAsync( new TimeSpan(0,0,0,0,timeoutMs), ct );
			}
		}


		class ReflectedScript : Disposable
		{
			public Guid Guid;

			public ScriptState State = new ScriptState();
			
			public List<ScriptStatusWatcher> Watchers = new List<ScriptStatusWatcher>();

			// when we first time detected the existence of a script
			public DateTime LastAliveTime = DateTime.Now;

			protected override void Dispose( bool disposing )
			{
				base.Dispose( disposing );
				if (!disposing) return;

				// dispose managed resources
				foreach (var w in Watchers)
				{
					w.Dispose();
				}
			}

			public void Tick()
			{
				// tick watchers, remove once complete
				var toRemove = new List<ScriptStatusWatcher>(10);
				foreach ( var i in Watchers )
				{
					i.Tick();
					if (i.ShallBeRemoved) toRemove.Add(i);
				}
				foreach (var i in toRemove)
				{
					Remove( i );
				}
			}

			void Remove( ScriptStatusWatcher w )
			{
				Watchers.Remove( w );
				w.Dispose();
			}

			public void OnScriptState( ScriptState state )
			{
				// on status change tell the watchers
				if( state.Status != State.Status )
				{
					foreach (var i in Watchers)
					{
						i.OnStatusChanged( state );
					}
				}

				// if anything changed (like the text), update our cached state (which is queried via IDirigent)
				if( state != State )
				{
					State.Status = state.Status;
					State.Text = state.Text;
					State.Data = state.Data;

					if (state.IsAlive)
					{
						LastAliveTime = DateTime.Now;
					}
				}
			}
		}

	}
}

