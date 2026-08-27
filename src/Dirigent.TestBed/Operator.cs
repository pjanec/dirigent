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

		/// <summary>The VFS nodes the master distributed, i.e. the top-level ones from the config.</summary>
		public Task<IReadOnlyList<VfsNodeDef>> GetAllVfsNodesAsync()
			=> InTick( () => (IReadOnlyList<VfsNodeDef>) _states.GetAllVfsNodesDef().ToList() );

		/// <summary>
		/// The definition of a node by its config id - what a GUI holds when the user clicks it.
		/// </summary>
		public async Task<VfsNodeDef> GetVfsNodeAsync( string id )
		{
			var all = await GetAllVfsNodesAsync();
			var found = all.Where( n => n.Id == id ).ToList();
			if( found.Count == 1 ) return found[0];

			throw new Exception( found.Count == 0
				? $"no VFS node with id '{id}'; known: {string.Join( ", ", all.Select( n => n.Id ) )}"
				: $"{found.Count} VFS nodes share the id '{id}'" );
		}
		/// <summary>
		/// Resolves a VFS node the way a context menu click would: variables expanded on the
		/// machine that owns the node, folders scanned, references followed.
		/// </summary>
		public async Task<VfsNodeDef?> ResolveAsync( VfsNodeDef node, bool forceUNC = false, bool includeContent = true )
		{
			// the resolution may itself await a script on another machine, so it must not run
			// inside the tick - only the call that starts it is marshalled
			var ctrl = await InTick( () => (IDirig) _states );
			return await OffPump( ctrl.ResolveAsync( node, forceUNC, includeContent ) );
		}

		/// <summary>
		/// Runs a script and returns its result - the same road the CLI takes with StartScript plus
		/// GetScriptState, minus the polling.
		/// </summary>
		/// <param name="hostId">empty = the master, which is where a GUI runs its scripts</param>
		public async Task<TResult?> RunScriptAsync<TArgs, TResult>(
				string scriptName, TArgs args, string hostId = "", TimeSpan? timeout = null )
		{
			// starting the script touches the script registry, so it belongs in the tick; the waiting
			// does not, and its completion has to be handed back to the pool - see OffPump
			var task = await InTick( () =>
				_states.ScriptReg.RunScriptAsync<TArgs, TResult>(
					hostId, scriptName, null, args, $"{scriptName} from a test", out var _ ) );

			var completion = OffPump( task );
			var finished = await Task.WhenAny( completion, Task.Delay( timeout ?? TimeSpan.FromSeconds( 60 ) ) );
			if( finished != completion )
				throw new TimeoutException( $"{scriptName} did not finish within the timeout" );

			return await completion;  // so a script failure surfaces here
		}

		/// <summary>
		/// Downloads a VFS node the way a click on a download action would: resolve the node here,
		/// then let the master's DownloadZipped script collect the files from every machine holding
		/// them into the download folder of the machine this operator sits on.
		/// </summary>
		/// <param name="perMachine">one archive per machine instead of a single merged one</param>
		public async Task<Scripts.BuiltIn.DownloadZipped.TResult> DownloadAsync(
				VfsNodeDef node, bool perMachine = false, TimeSpan? timeout = null )
		{
			var resolved = await ResolveAsync( node, forceUNC: false, includeContent: true );
			if( resolved is null )
				throw new Exception( $"nothing to download - '{node.Id}' resolved to nothing" );

			return await RunDownloadAsync(
				new Scripts.BuiltIn.DownloadZipped.TArgs() { VfsNode = resolved, PerMachine = perMachine },
				timeout );
		}

		/// <summary>
		/// Downloads a VFS node named by its config id - what a CLI or REST caller does, having no
		/// resolved tree to pass. The script resolves it itself.
		/// </summary>
		public Task<Scripts.BuiltIn.DownloadZipped.TResult> DownloadAsync(
				Scripts.BuiltIn.VfsNodeSelector node, bool perMachine = false, TimeSpan? timeout = null )
			=> RunDownloadAsync(
				new Scripts.BuiltIn.DownloadZipped.TArgs() { Node = node, PerMachine = perMachine },
				timeout );

		async Task<Scripts.BuiltIn.DownloadZipped.TResult> RunDownloadAsync(
				Scripts.BuiltIn.DownloadZipped.TArgs args, TimeSpan? timeout )
		{
			var result = await RunScriptAsync<Scripts.BuiltIn.DownloadZipped.TArgs, Scripts.BuiltIn.DownloadZipped.TResult>(
				Scripts.BuiltIn.DownloadZipped._Name, args, timeout: timeout );

			return result ?? throw new Exception( "the download returned no result at all" );
		}

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

		// ---- threading ---------------------------------------------------------------

		/// <summary>
		/// Hands the completion of a task back to the thread pool.
		/// </summary>
		/// <remarks>
		/// A script result arrives from inside the pump's tick, and ReflectedScriptRegistry completes
		/// its task right there, on the pump thread. Awaiting such a task directly would move the rest
		/// of the test body onto the pump thread - where the next Dispose would wait five seconds for a
		/// thread that is itself, and every read would run inside a tick rather than between ticks.
		/// Everything that awaits a script goes through here.
		/// </remarks>
		static Task<T> OffPump<T>( Task<T> task )
		{
			var tcs = new TaskCompletionSource<T>( TaskCreationOptions.RunContinuationsAsynchronously );

			task.ContinueWith( t =>
			{
				if( t.IsFaulted ) tcs.SetException( t.Exception!.InnerExceptions );
				else if( t.IsCanceled ) tcs.SetCanceled();
				else tcs.SetResult( t.Result );
			}, TaskContinuationOptions.ExecuteSynchronously );

			return tcs.Task;
		}
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
