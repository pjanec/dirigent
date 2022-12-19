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
	/// Interface used for dynamic instantiation of scripts
	/// </summary>
	public interface IScript : IDisposable
	{
		//void Tick();
		//bool HasFinished { get; set; }
		//void SetStatus( string? text, byte[]? data );
		string StatusText { get; }
		byte[]? StatusData { get; }
	}

	/// <summary>
	/// Script for executing tasks.
	/// Either built-in (in Dirigent), or dynamically copiled C#, or interpretted powershell
	/// </summary>
	public class Script : Disposable, IScript
	{
		public Guid Instance { get; set; }

		public string Title { get; set; } = string.Empty;

		public string Origin { get; set; } = string.Empty;

		public byte[]? Args { get; set; }

		/// <summary>
		/// Tries to deserialize the script arguments from the Args property.
		/// </summary>
		/// <returns>true if succeeded</returns>
		public bool TryGetArgs<T>( out T? args )
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

		public byte[] MakeResult<T>(T result) => Tools.Serialize<T>(result);

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

		public async Task<byte[]?> CallRun( CancellationToken ct ) => await Run(ct);

		/// <summary>
		/// Called after the Init().
		/// When finishes or cancelled, the Done() method is called and the script terminates.
		/// </summary>
		/// <param name="ct"></param>
		/// <returns></returns>
		protected async virtual Task<byte[]?> Run( CancellationToken ct )
		{
			await WaitUntilCancelled( ct );
			return null;
		}

		protected async Task WaitUntilCancelled( CancellationToken ct )
		{
			// if the script does not override this method,
			// we simply wait until the script is cancelled
			while( true )
			{
				await Task.Delay( 100, ct );
			}
		}

		protected virtual void SetStatus( string? text=null, byte[]? data=null )
		{
			lock( this )
			{
				// we do not allow setting the Status field directly from script; only the script controller can do that based on what is just happening to the script (init/run/finish...)
				_statusText = text;
				_statusData = data;
			}
		}


		protected Task StartApp( string id, string? planName, string? vars=null ) => Dirig.SendAsync( new Net.StartAppMessage( string.Empty, new AppIdTuple(id), planName, flags:0, vars:Tools.ParseEnvVarList(vars) ) );
		protected Task RestartApp( string id, string? vars=null ) => Dirig.SendAsync( new Net.RestartAppMessage( string.Empty, new AppIdTuple(id), vars:Tools.ParseEnvVarList(vars) ) );
		protected Task KillApp( string id ) => Dirig.SendAsync( new Net.KillAppMessage( string.Empty, new AppIdTuple(id) ) );
		protected Task<AppState?> GetAppState( string id ) => Dirig.GetAppStateAsync( new AppIdTuple( id ) );
		protected Task StartPlan( string id, string? vars=null ) => Dirig.SendAsync( new Net.StartPlanMessage( string.Empty, id, vars:Tools.ParseEnvVarList(vars) ) );
		protected Task RestartPlan( string id, string? vars=null ) => Dirig.SendAsync( new Net.RestartPlanMessage( string.Empty, id, vars:Tools.ParseEnvVarList(vars) ) );
		protected Task KillPlan( string id ) => Dirig.SendAsync( new Net.KillPlanMessage( string.Empty, id ) );
		protected Task<PlanState?> GetPlanState( string id ) => Dirig.GetPlanStateAsync( id );
		protected Task<ClientState?> GetClientState( string id ) => Dirig.GetClientStateAsync( id );
	}
}
