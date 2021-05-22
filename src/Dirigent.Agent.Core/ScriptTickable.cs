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

		public void StartApp( string id, string? planName )
		{
			_master.Send( new Net.StartAppMessage( string.Empty, new AppIdTuple(id), planName ) );
		}

		public void KillApp( string id )
		{
			_master.Send( new Net.KillAppMessage( string.Empty, new AppIdTuple(id) ) );
		}

		public AppState? GetAppState( string id )
		{
			return _master.GetAppState( new AppIdTuple( id ) );
		}

	}

	public interface IScript : ITickable
	{
		string Status { get; }
		ScriptCtrl Ctrl { get; set; }
		void Init();
		void Done();
	}

	public class Script : Disposable, IScript
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		
		public string Id { get; set; } = string.Empty;

		public bool ShallBeRemoved { get; protected set; }

		public uint Flags => 0;

		public string Status { get; protected set; } = string.Empty;

		// initialized during installation
		#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public ScriptCtrl Ctrl { get; set; }
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

		public virtual void Init()
		{
		}

		public virtual void Done()
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

		public virtual void Tick()
		{
		}

		public static void InitScriptInstance( IScript script, string id, Master master )
		{
			script.Id = id;
			script.Ctrl = new ScriptCtrl(master);
			script.Init();
		}

	}

}
