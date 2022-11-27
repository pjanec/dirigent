
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
	/// Creates a new task instance from the task definition.
	/// Maintains a list of active task instances.
	/// </summary>
	public class TaskRegistryAgent : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		// all currently running tasks
		public Dictionary<Guid, DTaskWorker> TaskWorkers { get; private set; } = new Dictionary<Guid, DTaskWorker>();
																					
		Agent _agent;

		public TaskRegistryAgent( Agent agent )
		{
			_agent = agent;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// dispose managed resources
			foreach (var t in TaskWorkers.Values)
			{
				t.Dispose();
			}
		}

		public void Tick()
		{
			foreach( var p in TaskWorkers.Values )
			{
				p.Tick();
			}
		}

		/// <summary>
		/// Creates a new task instance (if not existing yet) and starts it's worker part here on agent.
		/// </summary>
		/// <returns>Guid of the newly created task worker instance.</returns>
		public Guid StartWorker( string requestorId, Guid taskInstance, string taskId, string? args, string scriptName, string? scriptCode )
		{
			var task = new DTaskWorker( _agent, taskInstance, taskId, args );
			TaskWorkers[task.Guid] = task;

			var script = ScriptFactory.Create( taskInstance, taskId, scriptName, null, scriptCode, args, new SynchronousIDirig( _agent, _agent.SyncOps ) );

			task.Start( script, args );

			return task.Guid;
		}

		/// <summary>
		/// Kills all the active parts of this task's instance on the master as well as on the clients.
		/// </summary>
		public void Kill( string requestorId, Guid taskInstance )
		{
			if( !TaskWorkers.TryGetValue( taskInstance, out var task ) ) return;
			task.Kill();
		}

		public DTaskState? GetTaskState( Guid taskInstance )
		{
			if( !TaskWorkers.TryGetValue( taskInstance, out var task ) ) return null;
			return task.State;
		}

	}
}

