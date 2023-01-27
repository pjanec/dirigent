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
		byte[]? StatusData { get; }
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
		private byte[]? _statusData = null;
									
		string IScript.StatusText  => _statusText ?? "";
		byte[]? IScript.StatusData  => _statusData;

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;
		}

		public async Task<byte[]?> CallRun() => await Run();


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

		public byte[]? Args { get; set; }

		/// <summary> Who wanted this script to run </summary>
		public string Requestor { get; set; } = string.Empty;

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
		protected virtual Task<byte[]?> Run()
		{
			// by default we finish immediately with no result
			return Task.FromResult<byte[]?>(null);
		}

		/// <summary>
		/// Tries to deserialize the script arguments from the Args property.
		/// </summary>
		/// <returns>true if succeeded</returns>
		protected bool TryDeserializeArgs<T>( out T? args )
		{
			if (Args != null)
			{
				try
				{
					args = Tools.Deserialize<T>( Args );
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
		
		/// <summary>
		/// Serialize the results to be sent back to the caller waiting for this script.
		/// </summary>
		/// <typeparam name="T">Class hoding the result.</typeparam>
		/// <param name="result">Instanceof the class holding the result.</param>
		/// <returns>Serialized result.</returns>
		protected byte[] SerializeResult<T>(T result) => Tools.Serialize<T>(result);

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
		protected Task SetStatus( string? text=null, byte[]? data=null )
		{
			lock( this )
			{
				// we do not allow setting the Status field directly from script; only the script controller can do that based on what is just happening to the script (init/run/finish...)
				_statusText = text;
				_statusData = data;
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
		
		/// <summary>
		/// Gets the status of a dirigent plan.
		/// </summary>
		/// <param name="id">plan name</param>
		/// <returns>Null if nto such plan exists.</returns>
		protected Task<PlanState?> GetPlanState( string id ) => Dirig.GetPlanStateAsync( id );
		
		/// <summary>
		/// Gets the status of a script.
		/// </summary>
		/// <param name="id">Id if the script instance.</param>
		/// <returns>Null if there is not such script.</returns>
		protected Task<ScriptState?> GetScriptState( Guid id ) => Dirig.GetScriptStateAsync( id );
		
		/// <summary>
		/// Gets the status of machine.
		/// </summary>
		/// <param name="id">Name of the machine or the UUID of the connected client.</param>
		/// <returns>Null if there is no such machine/client known.</returns>
		protected Task<ClientState?> GetClientState( string id ) => Dirig.GetClientStateAsync( id );


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
		/// <param name="actionDef">What action to run. The action can be a tool (<see cref="ToolAppActionDef"/> or a script (<see cref="ScriptActionDef)"/>.</param>
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
	}
}
