
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
	public class TaskRegistryMaster : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Dictionary<string, DTaskDef> TaskDefs { get; private set; } = new Dictionary<string, DTaskDef>();

		// all currently running tasks
		public Dictionary<Guid, DTaskMaster> Tasks { get; private set; } = new Dictionary<Guid, DTaskMaster>();
																					
		public Dictionary<string, DTaskState> TaskStates => Tasks.Values.ToDictionary( p => p.Id, p => p.State );


		Master _master;
		public TaskRegistryMaster( Master master )
		{
			_master = master;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// dispose managed resources
			foreach (var t in Tasks.Values)
			{
				t.Dispose();
			}
		}

		public void SetTaskDefs( IEnumerable<DTaskDef> allDefs )
		{
			TaskDefs.Clear();
			foreach( var def in allDefs )
			{
				TaskDefs[def.Id] = def;
			}

		}

		/// <summary>
		/// Finds plan ba name. Throws if failed.
		/// </summary>
		public DTaskDef FindTaskDef( string id )
		{
			if (TaskDefs.TryGetValue( id, out var def ))
			{
				return def;
			}
			else
			{
				throw new Exception( $"TaskDef {id} was not found." );
			}
		}

		public void Tick()
		{
			foreach( var p in Tasks.Values )
			{
				p.Tick();
			}
		}

		/// <summary>
		/// Creates a new task instance and starts it's controller part here on master.
		/// </summary>
		/// <param name="requestorId"></param>
		/// <param name="id">what TaskDef to use</param>
		/// <param name="args">if null, use args from TaskDef</param>
		/// <returns>Guid of the newly created task instance.</returns>
		public Guid Start( string requestorId, string id, string? args )
		{
			var def = FindTaskDef( id );
			var task = new DTaskMaster( _master );
			Tasks[task.Guid] = task;
			task.Start( def, args );
			return task.Guid;
		}

		/// <summary>
		/// Kills all the active parts of this task's instance on the master as well as on the clients.
		/// </summary>
		public void Kill( string requestorId, Guid taskInstance )
		{
			if( !Tasks.TryGetValue( taskInstance, out var task ) ) return;
			task.Kill();
		}

		public DTaskState? GetTaskState( Guid taskInstance )
		{
			if( !Tasks.TryGetValue( taskInstance, out var task ) ) return null;
			return task.State;
		}

	}
}

