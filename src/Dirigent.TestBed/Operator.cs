using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.TestBed
{
	/// <summary>
	/// A GUI client with no GUI: it commands, observes, and receives the notifications a real
	/// operator would see. This is the seat the test sits in.
	/// </summary>
	/// <remarks>
	/// Everything a test calls here is marshalled into the pump thread's tick, because the
	/// client and the state repository are single-threaded by design. Reading them straight
	/// from the test thread is what produces the "collection was modified" failures.
	/// </remarks>
	public sealed class Operator : Disposable
	{
		public string Name { get; }

		/// <summary>Notifications the operator was shown, newest last.</summary>
		public IReadOnlyList<Net.UserNotificationMessage> Notifications
		{
			get { lock( _notificationsLock ) return _notifications.ToList(); }
		}

		readonly Net.Client _client;
		readonly ReflectedStateRepo _states;
		readonly SynchronousOpProcessor _syncOps = new();
		readonly List<Net.UserNotificationMessage> _notifications = new();
		readonly object _notificationsLock = new();

		internal Operator( string name, string masterIp, int masterPort, string rootForRelativePaths )
		{
			Name = name;

			var ident = new Net.ClientIdent() { Sender = name, SubscribedTo = Net.EMsgRecipCateg.Gui };
			_client = new Net.Client( ident, masterIp, masterPort, autoConn: true );
			_client.MessageReceived += OnMessage;

			// no local agent, hence no local machine
			_states = new ReflectedStateRepo( _client, string.Empty, rootForRelativePaths );
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;

			_client.MessageReceived -= OnMessage;
			_states.Dispose();
			_client.Dispose();
		}

		void OnMessage( Net.Message msg )
		{
			if( msg is Net.UserNotificationMessage notification )
			{
				lock( _notificationsLock ) _notifications.Add( notification );
			}
		}

		/// <summary>
		/// Called by the pump thread only. Client.Tick is what drains the socket and raises
		/// MessageReceived, so without it nothing is ever received.
		/// </summary>
		internal void Tick()
		{
			_client.Tick();
			_syncOps.Tick();
		}

		internal bool IsConnected => _client.IsConnected;

		// ---- observation -------------------------------------------------------------

		public Task<IReadOnlyList<KeyValuePair<AppIdTuple, AppState>>> GetAllAppsStateAsync()
			=> InTick( () => (IReadOnlyList<KeyValuePair<AppIdTuple, AppState>>)
					_states.GetAllAppsState().ToList() );

		public Task<AppState?> GetAppStateAsync( AppIdTuple id )
			=> InTick( () => _states.GetAppState( id ) );

		public Task<IReadOnlyList<KeyValuePair<AppIdTuple, AppDef>>> GetAllAppsDefAsync()
			=> InTick( () => (IReadOnlyList<KeyValuePair<AppIdTuple, AppDef>>)
					_states.GetAllAppsDef().ToList() );

		public Task<IReadOnlyList<KeyValuePair<string, ClientState>>> GetAllClientsStateAsync()
			=> InTick( () => (IReadOnlyList<KeyValuePair<string, ClientState>>)
					_states.GetAllClientsState().ToList() );

		public Task<IReadOnlyList<MachineDef>> GetAllMachinesDefAsync()
			=> InTick( () => (IReadOnlyList<MachineDef>) _states.GetAllMachinesDef().ToList() );

		// ---- commands ---------------------------------------------------------------

		/// <param name="planName">empty = no plan, use the standalone app definition</param>
		public Task StartAppAsync( AppIdTuple id, string? planName = "" )
			=> Send( new Net.StartAppMessage( Name, id, planName ) );

		public Task KillAppAsync( AppIdTuple id )
			=> Send( new Net.KillAppMessage( Name, id ) );

		public Task StartPlanAsync( string planName )
			=> Send( new Net.StartPlanMessage( Name, planName ) );

		public Task KillPlanAsync( string planName )
			=> Send( new Net.KillPlanMessage( Name, planName ) );

		public Task SendAsync( Net.Message msg ) => Send( msg );

		Task Send( Net.Message msg ) => InTick<object?>( () => { _states.Send( msg ); return null; } );

		// ---- marshalling ------------------------------------------------------------

		async Task<T> InTick<T>( Func<T> func )
		{
			var op = _syncOps.AddSynchronousOp( () => (object?) func() );
			await op.WaitAsync();
			if( op.Exception is not null )
				throw new Exception( $"operator call failed: {op.Exception.Message}", op.Exception );
			return (T) op.Result!;
		}
	}
}
