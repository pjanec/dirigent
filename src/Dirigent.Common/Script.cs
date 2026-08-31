using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace Dirigent
{
	/// <summary>
	/// Interface used by CSScript for dynamic instantiation of the script
	/// </summary>
	public interface IScript : IDisposable
	{
		string StatusText { get; }
		string? StatusData { get; }

		/// <summary> How far the script has got, 0..1, or null when it cannot say. </summary>
		double? StatusProgress { get; }
	}

	/// <summary>
	/// Script for executing remote tasks.
	/// Either built-in (in Dirigent), or dynamically compiled C#. Maybe, in the future, it could be a powershell too.
	/// </summary>
	public class Script : Disposable, IScript
	{
		// initialized during installation
		#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public IDirigAsync Dirig { get; set; }
		#pragma warning restore CS8618

		private string? _statusText = "";
		private string? _statusData = null;
		private double? _statusProgress = null;
									
		string IScript.StatusText  => _statusText ?? "";
		string? IScript.StatusData  => _statusData;
		double? IScript.StatusProgress  => _statusProgress;

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;
		}

		public async Task<string?> CallRun() => await Run();


		//////////////////////////////////
		//
		//  Script API
		//
		//////////////////////////////////

		//
		// The following members are initialized before the Run() is called. Can be used by the script any time.
		//

		public Guid Instance { get; set; }

		public string Title { get; set; } = string.Empty;

		public string Origin { get; set; } = string.Empty;

		public string Args { get; set; } = string.Empty;

		/// <summary> Who wanted this script to run </summary>
		public string Requestor { get; set; } = string.Empty;

		/// <summary>
		/// The marks of the files on this machine, or null if the host running this script keeps none.
		/// </summary>
		/// <remarks>
		/// Only an agent provides one, because only an agent owns files: the store is keyed by local
		/// path and lives beside the agent status file. A master or a GUI leaves it null, and the
		/// scripts read that as "nothing has been marked", which is the safe reading - a collection
		/// then takes whole files.
		/// </remarks>
		public FileMarkStore? MarkStore { get; set; }

		public CancellationToken CancellationToken;



		/// <summary>
		/// The body of the script. Called once the script was sucessfully instantiated.
		/// When finished or cancelled via the <see cref="CancellationToken"/>, the script terminates.
		/// </summary>
		/// <returns>Serialized result class instance. Script specific.</returns>
		/// <remarks>
		/// Runs in async context.
		/// Should not call any Dirigent's synchronous stuff directly, should always use the API calls defined here in the script class.
		/// </remarks>
		protected virtual Task<string?> Run()
		{
			// by default we finish immediately with no result
			return Task.FromResult<string?>(null);
		}

		protected string Serialize<T>( T? result )
		{
			return Tools.Serialize( result );
		}

		protected T? Deserialize<T>( string? json )
		{
			return Tools.Deserialize<T>( json );
		}

		/// <summary>
		/// Tries to deserialize given JSON string into given type, returns false if failed.
		/// </summary>
		/// <returns>true if succeeded</returns>
		protected bool TryDeserialize<T>( string serialized, out T? args )
		{
			if (Args != null)
			{
				try
				{
					args = Tools.Deserialize<T>( serialized );
					return true;
				}
				catch
				{
					args = default;
					return false;
				}
			}
			else
			{
				args = default;
				return false;
			}
		}

		protected async Task WaitUntilCancelled()
		{
			// if the script does not override this method,
			// we simply wait until the script is cancelled
			while( true )
			{
				await Task.Delay( 100, CancellationToken );
			}
		}

		/// <summary>
		/// Updates the script status text and optional data.
		/// </summary>
		/// <param name="text">A status message. A brief text description of a status.</param>
		/// <param name="data">Optional extra data. Script specific. Caller needs to understand the format in order to use it.</param>
		/// <returns></returns>
		/// <remarks>The status is sent back to the caller who initiated the script so it can track the script progress.</remarks>
		/// <param name="progress">
		/// How far the script has got, 0..1. Null says the script cannot tell, and whoever shows it
		/// then indicates that rather than inventing a number.
		/// </param>
		protected Task SetStatus( string? text=null, string? data=null, double? progress=null )
		{
			lock( this )
			{
				// we do not allow setting the Status field directly from script; only the script controller can do that based on what is just happening to the script (init/run/finish...)
				_statusText = text;
				_statusData = data;
				_statusProgress = progress;
			}

			return Task.CompletedTask;
		}


		/// <summary>
		/// Waits for specified time.
		/// </summary>
		/// <param name="msecs">number of millisecods to wait for</param>
		protected Task Wait( int msecs ) => Task.Delay(msecs, CancellationToken);
		
		/// <summary>
		/// Starts an application. Just sends the command and returns immediately.
		/// </summary>
		/// <param name="id">Name of the app.</param>
		/// <param name="planName">
		///		If null, the settings for current plan are used.
		///		If empty string, the settings for the default app defined outside of a plan are used.</param>
		/// <param name="vars">
		///		Extra variables to set to the app's environment.
		/// </param>
		/// <remarks>
		/// This does not wait until the apps has started. To do so, you would need to poll the application status.
		/// </remarks>
		protected Task StartApp( string id, string? planName, string? vars=null ) => Dirig.SendAsync( new Net.StartAppMessage( string.Empty, new AppIdTuple(id), planName, flags:0, vars:Tools.ParseEnvVarList(vars) ) );
		protected Task RestartApp( string id, string? vars=null ) => Dirig.SendAsync( new Net.RestartAppMessage( string.Empty, new AppIdTuple(id), vars:Tools.ParseEnvVarList(vars) ) );
		protected Task KillApp( string id ) => Dirig.SendAsync( new Net.KillAppMessage( string.Empty, new AppIdTuple(id) ) );
		
		protected Task StartPlan( string id, string? vars=null ) => Dirig.SendAsync( new Net.StartPlanMessage( string.Empty, id, vars:Tools.ParseEnvVarList(vars) ) );
		protected Task RestartPlan( string id, string? vars=null ) => Dirig.SendAsync( new Net.RestartPlanMessage( string.Empty, id, vars:Tools.ParseEnvVarList(vars) ) );
		protected Task KillPlan( string id ) => Dirig.SendAsync( new Net.KillPlanMessage( string.Empty, id ) );
		
		/// <summary>
		/// Gets the status of a dirigent controlled aplication.
		/// </summary>
		/// <param name="id">app name in form of 'machine.app'</param>
		/// <returns>Null if no such app is defined.</returns>
		protected Task<AppState?> GetAppState( string id ) => Dirig.GetAppStateAsync( new AppIdTuple( id ) );
		
		protected Task<IEnumerable<KeyValuePair<AppIdTuple, AppState>>> GetAllAppsState() => Dirig.GetAllAppsStateAsync();

		/// <summary>
		/// Gets the status of a dirigent plan.
		/// </summary>
		/// <param name="id">plan name</param>
		/// <returns>Null if nto such plan exists.</returns>
		protected Task<PlanState?> GetPlanState( string id ) => Dirig.GetPlanStateAsync( id );
		
		protected Task<IEnumerable<KeyValuePair<string, PlanState>>> GetAllPlansState() => Dirig.GetAllPlanStatesAsync();

		/// <summary>
		/// Gets the status of a script.
		/// </summary>
		/// <param name="id">Id if the script instance.</param>
		/// <returns>Null if there is not such script.</returns>
		protected Task<ScriptState?> GetScriptState( Guid id ) => Dirig.GetScriptStateAsync( id );

		protected Task<IEnumerable<KeyValuePair<Guid, ScriptState>>> GetAllScriptsState() => Dirig.GetAllScriptsStateAsync();
		
		/// <summary>
		/// Gets the status of machine.
		/// </summary>
		/// <param name="id">Name of the machine or the UUID of the connected client.</param>
		/// <returns>Null if there is no such machine/client known.</returns>
		protected Task<ClientState?> GetClientState( string id ) => Dirig.GetClientStateAsync( id );

		protected Task<IEnumerable<KeyValuePair<string, ClientState>>> GetAllClientsState() => Dirig.GetAllClientsStateAsync();

		/// <summary>
		/// Starts a script on given machine and waits until it finishes.
		/// </summary>
		/// <typeparam name="TArgs">Class holding the script arguments.</typeparam>
		/// <typeparam name="TResult">Class holding the script results.</typeparam>
		/// <param name="clientId">What machine to start the script on.</param>
		/// <param name="scriptName">Name of the script, either the file name or the name of a built-in script.</param>
		/// <param name="sourceCode">Optional source code; if empty, the script needs to be available on the target machine.</param>
		/// <param name="args">Instance of the script argument class.</param>
		/// <param name="title">Script name for debug prints and UI presentation purposes.</param>
		/// <param name="scriptInstance">The id of the script instance started.</param>
		/// <returns>
		///		Instance of the script result class. The called script needs to serialize it using <see cref="SerializeResult{T}(T)"/>.
		///	</returns>
		protected Task<TResult?> RunScriptAndWait<TArgs, TResult>(
			string clientId,
			string scriptName,
			string? sourceCode,
			TArgs? args,
			string title,
			out Guid scriptInstance
			)
			=> Dirig.RunScriptAsync<TArgs, TResult>( clientId, scriptName, sourceCode, args, title, out scriptInstance );


		/// <summary>
		/// Runs an action on given host machine. Just sends a command and returns immediately.
		/// </summary>
		/// <param name="requestorId">Client name of where to report potential trouble.</param>
		/// <param name="actionDef">What action to run. The action can be a tool (<see cref="ToolActionDef"/> or a script (<see cref="ScriptActionDef)"/>.</param>
		/// <param name="hostClientId">Machine where to run the action.</param>
		/// <param name="vars">Optional variables passed to the action. They are used for expansion in the exe path and args.</param>
		/// <returns></returns>
		/// 
		protected Task RunAction(
			string requestorId,
			ActionDef actionDef,
			string hostClientId,
			Dictionary<string,string>? vars=null
			)
			=> Dirig.SendAsync( new Net.RunActionMessage( requestorId, actionDef, hostClientId, vars ) );

		/// <summary>
		/// Gets the definition of an app as was last time started.
		/// </summary>
		/// <param name="id">machine.app tuple</param>
		/// <returns>Null if there is no such app.</returns>
		protected Task<AppDef?> GetAppDef( AppIdTuple id ) => Dirig.GetAppDefAsync( id );
		
		protected Task<IEnumerable<KeyValuePair<AppIdTuple, AppDef>>> GetAllAppsDef() => Dirig.GetAllAppsDefAsync();

		/// <summary>
		/// Gets the definition of a plan.
		/// </summary>
		/// <param name="id">plan name</param>
		/// <returns>Null if there is no such plan.</returns>
		protected Task<PlanDef?> GetPlanDef( string id ) => Dirig.GetPlanDefAsync( id );

		protected Task<IEnumerable<PlanDef>> GetAllPlansDef => Dirig.GetAllPlansDefAsync();

		/// <summary>
		/// Gets the definition of a script from SharedConfig.
		/// </summary>
		protected Task<ScriptDef?> GetScriptDef( Guid Id ) => Dirig.GetScriptDefAsync(Id);

		protected Task<IEnumerable<ScriptDef>> GetAllScriptsDef() => Dirig.GetAllScriptsDefAsync();

	}
}
