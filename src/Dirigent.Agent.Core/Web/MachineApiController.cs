using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;

namespace Dirigent.Web
{
	/// <summary>A machine as the configuration declares it and as the system sees it now.</summary>
	public class MachineInfo
	{
		public string id { get; set; } = string.Empty;

		/// <summary>The IP address SharedConfig gives; null if none (the address is then learned from the connection).</summary>
		public string? ip { get; set; }

		public bool connected { get; set; }

		/// <summary>The address the agent connected from; null if it never has.</summary>
		public string? actualIp { get; set; }

		/// <summary>When the connection state last changed (UTC); null if the machine never connected.</summary>
		public DateTime? lastChange { get; set; }

		/// <summary>The MAC address SharedConfig gives; null if none.</summary>
		public string? mac { get; set; }

		/// <summary>The MAC addresses the agent reported when it last connected - what a wake-up uses unless SharedConfig gives one.</summary>
		public List<string> macLearned { get; set; } = new();

		public List<MachineShare> shares { get; set; } = new();

		/// <summary>Ids of the file definitions that belong to the machine as such, not to one of its applications.</summary>
		public List<string> fileDefs { get; set; } = new();

		/// <summary>Ids ("machine.app") of the applications defined for this machine.</summary>
		public List<string> apps { get; set; } = new();
	}

	public class MachineShare
	{
		public string name { get; set; } = string.Empty;
		public string path { get; set; } = string.Empty;
	}

	/// <summary>How many of each thing the shared config in use defines.</summary>
	public class SharedConfigStatus
	{
		public string path { get; set; } = string.Empty;
		public DateTime loadedAt { get; set; }
		public string? minDirigentVersion { get; set; }
		public int machines { get; set; }
		public int apps { get; set; }
		public int plans { get; set; }
		public int scripts { get; set; }
		public int fileDefs { get; set; }
	}

	/// <summary>The answer of GET /api/status.</summary>
	public class MasterStatus
	{
		/// <summary>As <c>--version</c> prints it.</summary>
		public string version { get; set; } = string.Empty;
		public string masterMachineId { get; set; } = string.Empty;
		public string? masterIp { get; set; }
		public int httpPort { get; set; }
		public DateTime startedAt { get; set; }
		public SharedConfigStatus sharedConfig { get; set; } = new();
		public string? localConfigPath { get; set; }
		public string archiveFolder { get; set; } = string.Empty;
		public bool killAllInProgress { get; set; }

		/// <summary>The ids of the clients (agents, GUIs) connected now.</summary>
		public List<string> connectedClients { get; set; } = new();
	}

	/// <summary>
	/// What describes the site to a tool - the master (<c>/api/status</c>) and its machines (<c>/api/machinedefs</c>) - and the power
	/// control of a machine. See docs/HTTP.md, "Describing the site".
	/// </summary>
	public class MachineApiController : WebApiController
	{
		readonly Master _master;

		public MachineApiController( Master master )
		{
			_master = master;
		}

		static string? NullIfEmpty( string? s ) => string.IsNullOrEmpty( s ) ? null : s;

		/// <summary>Master thread only.</summary>
		public MachineInfo InfoOf( MachineDef def )
		{
			var state = _master.GetClientState( def.Id );

			var info = new MachineInfo()
			{
				id = def.Id,
				ip = NullIfEmpty( def.IP ),
				connected = state?.Connected ?? false,
				actualIp = NullIfEmpty( state?.IP ),
				lastChange = state is null || state.LastChange == DateTime.MinValue ? null : state.LastChange,
				mac = NullIfEmpty( def.MAC ),
				macLearned = _master.GetLearnedMacs( def.Id ),
				shares = def.FileShares.Select( s => new MachineShare() { name = s.Name, path = s.Path } ).ToList(),
				fileDefs = _master.GetAllVfsNodesDef()
								.Where( n => n.MachineId == def.Id && string.IsNullOrEmpty( n.AppId ) )
								.Select( n => n.Id ).Distinct().OrderBy( x => x ).ToList(),
				apps = _master.GetAllAppsDef().Where( kv => kv.Key.MachineId == def.Id ).Select( kv => kv.Key.ToString() ).OrderBy( x => x ).ToList(),
			};
			return info;
		}

		[Route( HttpVerbs.Get, "/machinedefs" )]
		public async Task GetMachineDefs()
		{
			var list = await ApiOps.OnMaster( _master, () => _master.GetAllMachinesDef().Select( InfoOf ).ToList() );
			await SendJson( AppFilesResult.ToJson( list ) );
		}

		[Route( HttpVerbs.Get, "/machinedefs/{id}" )]
		public async Task GetMachineDef( string id )
		{
			var info = await ApiOps.OnMaster( _master, () =>
			{
				var def = _master.GetMachineDef( id ) ?? throw HttpException.NotFound( $"No machine '{id}' is defined." );
				return InfoOf( def );
			} );
			await SendJson( AppFilesResult.ToJson( info ) );
		}

		[Route( HttpVerbs.Post, "/machinedefs/{id}/shutdown" )]
		public Task Shutdown( string id ) => Power( id, "shut down", EShutdownMode.PowerOff );

		[Route( HttpVerbs.Post, "/machinedefs/{id}/reboot" )]
		public Task Reboot( string id ) => Power( id, "reboot", EShutdownMode.Reboot );

		async Task Power( string id, string what, EShutdownMode mode )
		{
			await ApiOps.OnMaster( _master, () =>
			{
				if( _master.GetMachineDef( id ) is null ) throw HttpException.NotFound( $"No machine '{id}' is defined." );

				var state = _master.GetClientState( id );
				if( state is null || !state.Connected )
					throw new Exception( $"Machine {id} is not connected, so it cannot be told to {what}." );

				_master.Shutdown( "", new ShutdownArgs() { Mode = mode }, id );
			} );

			await Accepted( $"Machine {id} was told to {what}." );
		}

		[Route( HttpVerbs.Post, "/machinedefs/{id}/wakeup" )]
		public async Task WakeUp( string id )
		{
			await ApiOps.OnMaster( _master, () =>
			{
				if( _master.GetMachineDef( id ) is null ) throw HttpException.NotFound( $"No machine '{id}' is defined." );

				if( _master.GetClientState( id )?.Connected ?? false )
					throw new Exception( $"Machine {id} is already online." );

				_master.WakeUp( "", id );
			} );

			await Accepted( $"Wake-on-LAN was sent to machine {id}; it may take a minute to come up." );
		}

		async Task Accepted( string message )
		{
			HttpContext.Response.StatusCode = (int) HttpStatusCode.Accepted;
			await SendJson( AppFilesResult.ToJson( new { message } ) );
		}

		/// <summary>What GET /api/status answers. Master thread only.</summary>
		public MasterStatus StatusOf()
			=> new MasterStatus()
		{
			version = AppConfig.VersionText(),
			masterMachineId = _master.MachineId,
			masterIp = NullIfEmpty( _master.LocalIp ),
			httpPort = _master.HttpPort,
			startedAt = _master.StartedAt,
			sharedConfig = new SharedConfigStatus()
			{
				path = _master.SharedConfigFile,
				loadedAt = _master.SharedConfigLoadedAt,
				minDirigentVersion = NullIfEmpty( _master.MinDirigentVersion ),
				machines = _master.GetAllMachinesDef().Count(),
				apps = _master.GetAllAppsDef().Count(),
				plans = _master.GetAllPlansDef().Count(),
				scripts = _master.GetAllScriptsDef().Count(),
				fileDefs = _master.GetAllVfsNodesDef().Count(),
			},
			localConfigPath = NullIfEmpty( _master.LocalConfigFile ),
			archiveFolder = _master.DownloadJobs.ArchiveFolder,
			killAllInProgress = _master.KillAllInProgress,
			connectedClients = _master.GetAllClientsState().Where( kv => kv.Value.Connected ).Select( kv => kv.Key ).OrderBy( x => x ).ToList(),
		};

		[Route( HttpVerbs.Get, "/status" )]
		public async Task GetStatus()
		{
			var status = await ApiOps.OnMaster( _master, StatusOf );

			await SendJson( AppFilesResult.ToJson( status ) );
		}

		Task SendJson( string json ) => HttpContext.SendStringAsync( json, "application/json", System.Text.Encoding.UTF8 );
	}
}
