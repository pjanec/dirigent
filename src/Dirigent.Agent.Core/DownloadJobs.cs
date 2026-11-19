using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Dirigent.Web;

namespace Dirigent
{
	/// <summary>The node a download names, the way scripts address nodes.</summary>
	public class DownloadNodeSelection
	{
		public string? id { get; set; }
		public string? machine { get; set; }
		public string? app { get; set; }
	}

	/// <summary>What was asked for: a node or package by id, or all the nodes of an application.</summary>
	public class DownloadRequest
	{
		public DownloadNodeSelection? node { get; set; }

		/// <summary>"m1.a" or "m1.a@plan"</summary>
		public string? app { get; set; }

		public string? comment { get; set; }
	}

	/// <summary>What job.json holds.</summary>
	public class DownloadJobRecord
	{
		public string id { get; set; } = string.Empty;
		public DownloadRequest request { get; set; } = new();
		public DateTime created { get; set; }
		public DateTime? finished { get; set; }

		/// <summary>Running, Finished, Failed or Cancelled</summary>
		public string status { get; set; } = "Running";

		/// <summary>The machine the archives are on - the master's.</summary>
		public string machine { get; set; } = string.Empty;

		/// <summary>File names, relative to the job folder.</summary>
		public List<string> files { get; set; } = new();
		public List<string> errors { get; set; } = new();
		public List<string> notCollected { get; set; } = new();
	}

	public class DownloadJobFile
	{
		public string name { get; set; } = string.Empty;
		public long size { get; set; }
		public string url { get; set; } = string.Empty;
		public string localPath { get; set; } = string.Empty;
		public string? uncPath { get; set; }
		public string? uncError { get; set; }
	}

	/// <summary>A job as the REST API and the CLI answer about it.</summary>
	public class DownloadJobView
	{
		public string id { get; set; } = string.Empty;

		/// <summary>Running, Finished, Failed, Cancelled or Orphaned (running when the master went away)</summary>
		public string status { get; set; } = string.Empty;
		public double? progress { get; set; }
		public string? text { get; set; }
		public DateTime created { get; set; }
		public DateTime? finished { get; set; }
		public string machine { get; set; } = string.Empty;
		public DownloadRequest request { get; set; } = new();
		public List<DownloadJobFile> files { get; set; } = new();
		public List<string> errors { get; set; } = new();
		public List<string> notCollected { get; set; } = new();

		public string ToJson() => AppFilesResult.ToJson( this );
	}

	public enum EDownloadJobProblem { NotFound, Forbidden, Conflict }

	public class DownloadJobException : Exception
	{
		public EDownloadJobProblem Problem { get; }
		public DownloadJobException( EDownloadJobProblem problem, string message ) : base( message ) { Problem = problem; }
	}

	/// <summary>
	/// The download jobs of the master: collections requested over REST or the CLI, built on the
	/// master's machine into a folder of their own, kept for a while, and cleaned up after.
	/// </summary>
	/// <remarks>
	/// A job is `&lt;ArchiveFolder&gt;\&lt;guid&gt;\` holding `job.json` and what DownloadZipped made. The
	/// guid is the instance id of the script doing the collection. Starting, killing and the ticking
	/// belong to the master thread; reading a job (status, files, listing) is safe from any thread,
	/// because job.json is only ever replaced whole.
	/// </remarks>
	public class DownloadJobs
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public const string JobFileName = "job.json";

		static readonly JsonSerializerOptions _readOptions = new() { PropertyNameCaseInsensitive = true };

		public string ArchiveFolder { get; }
		public int RetentionHours { get; }
		public long MaxBytes { get; }
		public bool ServeArchives { get; }

		readonly Master? _master;
		readonly Func<Guid, bool>? _isRunningExtra;
		readonly object _lock = new();

		class Running
		{
			public DateTime Started = DateTime.UtcNow;
			public double? Progress;
			public string? Text;
		}

		readonly Dictionary<Guid, Running> _running = new();
		readonly HashSet<Guid> _deleteWhenDone = new();
		DateTime _lastCleanup = DateTime.UtcNow;

		/// <param name="isRunningExtra">another say on whether a job runs now; for tests of the cleanup</param>
		public DownloadJobs( string archiveFolder, int retentionHours, long maxBytes, bool serveArchives,
				Master? master = null, Func<Guid, bool>? isRunningExtra = null )
		{
			ArchiveFolder = archiveFolder;
			RetentionHours = retentionHours;
			MaxBytes = maxBytes;
			ServeArchives = serveArchives;
			_master = master;
			_isRunningExtra = isRunningExtra;
		}

		/// <summary>The folder from the settings: ArchiveFolder, else Archives beside the agent status file.</summary>
		public static string FolderFrom( AppConfig ac )
		{
			if( !string.IsNullOrEmpty( ac.ArchiveFolder ) ) return ac.ArchiveFolder;

			var status = ac.AgentStatusFolder;
			if( string.IsNullOrEmpty( status ) )
				status = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "Dirigent" );

			return Path.Combine( status, "Archives" );
		}

		public string FolderOf( Guid id ) => Path.Combine( ArchiveFolder, id.ToString() );

		bool IsRunning( Guid id )
		{
			lock( _lock )
			{
				if( _running.ContainsKey( id ) ) return true;
			}
			return _isRunningExtra?.Invoke( id ) ?? false;
		}

		// ---- starting and ending ------------------------------------------------------------------

		/// <summary>
		/// Starts the collection. Master thread only.
		/// </summary>
		/// <exception cref="AppFilesNotFoundException">the node, app or plan does not exist</exception>
		public Guid Start( DownloadRequest request )
		{
			var master = _master ?? throw new InvalidOperationException( "no master" );

			Cleanup();

			var args = new Scripts.BuiltIn.DownloadZipped.TArgs()
			{
				ToMachine = master.MachineId,
				Comment = request.comment,
			};

			if( request.node is not null && !string.IsNullOrEmpty( request.node.id ) )
			{
				var selector = new Scripts.BuiltIn.VfsNodeSelector()
				{
					Id = request.node.id,
					MachineId = request.node.machine,
					AppId = request.node.app,
				};

				if( !master.GetAllVfsNodesDef().Any( n => Scripts.BuiltIn.ListVfsNodes.Matches( n, selector ) ) )
					throw new AppFilesNotFoundException( $"No node matches {selector}." );

				args.Node = selector;
			}
			else
			if( !string.IsNullOrEmpty( request.app ) )
			{
				var query = AppFilesQuery.Prepare( master, request.app );

				// the definition goes over as it is; DownloadZipped resolves it itself, with progress
				args.VfsNode = new VFolderDef()
				{
					Guid = Guid.NewGuid(),
					Id = query.AppIdText,
					Title = query.AppIdText,
					IsContainer = true,
					Children = query.Nodes.ToList(),
				};
				args.VfsNodeNeedsResolving = true;
			}
			else
			{
				throw new ArgumentException( "Either 'node' (with an id) or 'app' is needed." );
			}

			var id = Guid.NewGuid();
			var folder = FolderOf( id );
			Directory.CreateDirectory( folder );
			args.ToFolder = folder;

			WriteRecord( folder, new DownloadJobRecord()
			{
				id = id.ToString(),
				request = request,
				created = DateTime.UtcNow,
				status = "Running",
				machine = master.MachineId,
			} );

			lock( _lock ) _running[id] = new Running();

			try
			{
				master.StartSingletonScript( "", id, Scripts.BuiltIn.DownloadZipped._Name, Tools.Serialize( args ) );
			}
			catch( Exception e )
			{
				Finish( id, new ScriptState( EScriptStatus.Failed, null,
					Tools.Serialize( new SerializedException() { Message = e.Message } ) ) );
				throw;
			}

			return id;
		}

		/// <summary>Follows the running jobs and tidies up now and then. Master thread.</summary>
		public void Tick()
		{
			if( _master is null ) return;

			List<Guid> ids;
			lock( _lock ) ids = _running.Keys.ToList();

			foreach( var id in ids )
			{
				ScriptState? state = null;
				try { state = _master.GetScriptState( "", id ); }
				catch( Exception e ) { log.Debug( $"Download {id}: {e.Message}" ); }

				Running? job;
				lock( _lock ) _running.TryGetValue( id, out job );
				if( job is null ) continue;

				if( state is null )
				{
					if( DateTime.UtcNow - job.Started > TimeSpan.FromSeconds( 30 ) )
						Finish( id, new ScriptState( EScriptStatus.Failed, null,
							Tools.Serialize( new SerializedException() { Message = "The download script did not start." } ) ) );
					continue;
				}

				if( state.IsAlive )
				{
					lock( _lock ) { job.Progress = state.Progress; job.Text = state.Text; }
					continue;
				}

				Finish( id, state );
			}

			if( DateTime.UtcNow - _lastCleanup > TimeSpan.FromHours( 1 ) )
				Cleanup();
		}

		void Finish( Guid id, ScriptState state )
		{
			var folder = FolderOf( id );

			bool deleteIt;
			lock( _lock ) deleteIt = _deleteWhenDone.Remove( id );

			try
			{
				if( deleteIt )
				{
					TryDeleteFolder( folder );
				}
				else
				{
					var record = ReadRecord( folder ) ?? new DownloadJobRecord() { id = id.ToString(), created = DateTime.UtcNow };
					record.finished = DateTime.UtcNow;

					switch( state.Status )
					{
						case EScriptStatus.Finished:
							record.status = "Finished";
							var result = Tools.Deserialize<Scripts.BuiltIn.DownloadZipped.TResult>( state.Data );
							if( result is not null )
							{
								record.machine = string.IsNullOrEmpty( result.DownloadMachine ) ? record.machine : result.DownloadMachine;
								record.files = result.Files.Select( f => Path.GetFileName( f ) ).ToList();
								record.errors = result.Errors;
								record.notCollected = result.NotCollected;
							}
							break;

						case EScriptStatus.Cancelled:
							record.status = "Cancelled";
							break;

						default:
							record.status = "Failed";
							record.errors.Add( ErrorOf( state ) );
							break;
					}

					WriteRecord( folder, record );
				}
			}
			catch( Exception e )
			{
				log.Error( $"Could not finish the download job {id}: {e.Message}" );
			}
			finally
			{
				lock( _lock ) _running.Remove( id );
			}
		}

		static string ErrorOf( ScriptState state )
		{
			try
			{
				var ex = Tools.Deserialize<SerializedException>( state.Data );
				if( ex is not null && !string.IsNullOrEmpty( ex.Message ) ) return Tools.JustFirstLine( ex.Message );
			}
			catch { }
			return string.IsNullOrEmpty( state.Text ) ? $"The download ended as {state.Status}." : state.Text!;
		}

		// ---- asking -------------------------------------------------------------------------------

		/// <summary>True while the job's script runs. Any thread.</summary>
		public bool IsJobRunning( Guid id ) => IsRunning( id );

		/// <summary>One job, or null if there is no such job. Any thread.</summary>
		public DownloadJobView? Get( Guid id )
		{
			// in this order: a job that has ended is no longer running and its record is final
			bool running;
			Running? live = null;
			lock( _lock ) { running = _running.TryGetValue( id, out live ); }
			running = running || ( _isRunningExtra?.Invoke( id ) ?? false );

			var record = ReadRecord( FolderOf( id ) );
			if( record is null ) return null;

			return ViewOf( id, record, running, live );
		}

		/// <summary>All the jobs on disk, newest first. Any thread.</summary>
		public List<DownloadJobView> List()
		{
			var all = new List<DownloadJobView>();

			foreach( var (id, _) in OwnedFolders() )
			{
				var view = Get( id );
				if( view is not null ) all.Add( view );
			}

			return all.OrderByDescending( v => v.created ).ToList();
		}

		DownloadJobView ViewOf( Guid id, DownloadJobRecord record, bool running, Running? live )
		{
			var folder = FolderOf( id );

			var view = new DownloadJobView()
			{
				id = id.ToString(),
				status = record.status == "Running" && !running ? "Orphaned" : record.status,
				created = record.created,
				finished = record.finished,
				machine = record.machine,
				request = record.request,
				errors = record.errors,
				notCollected = record.notCollected,
			};

			if( running && live is not null )
			{
				lock( _lock ) { view.progress = live.Progress; view.text = live.Text; }
			}
			else
			if( record.status == "Finished" )
			{
				view.progress = 1.0;
			}

			foreach( var name in record.files )
			{
				var path = Path.Combine( folder, name );

				var file = new DownloadJobFile()
				{
					name = name,
					size = File.Exists( path ) ? new FileInfo( path ).Length : 0,
					url = $"/api/downloads/{id}/files/{Uri.EscapeDataString( name )}",
					localPath = path,
				};

				if( _master is not null )
				{
					try { file.uncPath = _master.MakeUNC( path, record.machine, name ); }
					catch( Exception e ) { file.uncError = Tools.JustFirstLine( e.Message ); }
				}

				view.files.Add( file );
			}

			return view;
		}

		/// <summary>
		/// The full path of one archive of a job, for serving it. Any thread.
		/// </summary>
		/// <exception cref="DownloadJobException">unknown job or file, serving switched off, or the job still running</exception>
		public string FileToServe( Guid id, string name )
		{
			if( !ServeArchives )
				throw new DownloadJobException( EDownloadJobProblem.Forbidden, "Serving the archives is switched off (--serveArchives 0)." );

			var view = Get( id ) ?? throw new DownloadJobException( EDownloadJobProblem.NotFound, $"No download {id}." );

			// first, because a running job has no files listed yet: asking for one of them is "not yet", not "no such file"
			if( view.status == "Running" )
				throw new DownloadJobException( EDownloadJobProblem.Conflict, $"Download {id} is still running." );

			// only what the job says it made; a name with a path in it can never be one of those
			if( string.IsNullOrEmpty( name ) || name != Path.GetFileName( name )
				|| !view.files.Any( f => string.Equals( f.name, name, StringComparison.Ordinal ) ) )
				throw new DownloadJobException( EDownloadJobProblem.NotFound, $"Download {id} has no file '{name}'." );

			var path = Path.Combine( FolderOf( id ), name );
			if( !File.Exists( path ) )
				throw new DownloadJobException( EDownloadJobProblem.NotFound, $"The file '{name}' of download {id} is gone." );

			return path;
		}

		/// <summary>
		/// Stops the job if it runs, and removes it: at once if it has ended, when the script has
		/// stopped if not. Master thread only.
		/// </summary>
		public void Delete( Guid id )
		{
			var folder = FolderOf( id );

			if( ReadRecord( folder ) is null )
				throw new DownloadJobException( EDownloadJobProblem.NotFound, $"No download {id}." );

			bool running;
			lock( _lock ) running = _running.ContainsKey( id );

			if( running )
			{
				lock( _lock ) _deleteWhenDone.Add( id );
				_master?.KillScript( "", id ); // the folder goes when the script has really stopped
				return;
			}

			if( !TryDeleteFolder( folder ) )
				throw new Exception( $"Could not delete the folder of download {id}; it is in use." );
		}

		// ---- cleaning up --------------------------------------------------------------------------

		/// <summary>
		/// Removes what has outlived the retention time, then the oldest finished jobs while the total is
		/// over the cap. Touches only folders named by a guid that hold a job.json, and never a job that
		/// runs now. A folder that cannot be deleted is left for the next pass.
		/// </summary>
		public void Cleanup()
		{
			_lastCleanup = DateTime.UtcNow;

			var jobs = new List<(Guid id, string folder, DateTime when, long size)>();
			long total = 0;

			foreach( var (id, folder) in OwnedFolders() )
			{
				var size = SizeOf( folder );
				total += size;

				if( IsRunning( id ) ) continue;

				var record = ReadRecord( folder );

				// a job that says "running" but is not running now is an orphan; its age is that of its start
				var when = record is null
								? Directory.GetCreationTimeUtc( folder )
								: ( record.status != "Running" ? record.finished : null ) ?? record.created;

				if( when.Kind == DateTimeKind.Local ) when = when.ToUniversalTime();

				if( DateTime.UtcNow - when >= TimeSpan.FromHours( RetentionHours ) )
				{
					if( TryDeleteFolder( folder ) ) total -= size;
				}
				else
				{
					jobs.Add( ( id, folder, when, size ) );
				}
			}

			if( MaxBytes > 0 && total > MaxBytes )
			{
				foreach( var job in jobs.OrderBy( j => j.when ) )
				{
					if( total <= MaxBytes ) break;
					if( TryDeleteFolder( job.folder ) ) total -= job.size;
				}
			}
		}

		/// <summary>The subfolders that are ours: named by a guid, and holding a job.json.</summary>
		IEnumerable<(Guid id, string folder)> OwnedFolders()
		{
			if( !Directory.Exists( ArchiveFolder ) ) yield break;

			foreach( var folder in Directory.GetDirectories( ArchiveFolder ) )
			{
				if( !Guid.TryParseExact( Path.GetFileName( folder ), "D", out var id ) ) continue;
				if( !File.Exists( Path.Combine( folder, JobFileName ) ) ) continue;
				yield return ( id, folder );
			}
		}

		static long SizeOf( string folder )
		{
			try
			{
				return new DirectoryInfo( folder ).EnumerateFiles( "*", SearchOption.AllDirectories ).Sum( f => f.Length );
			}
			catch( Exception )
			{
				return 0;
			}
		}

		/// <remarks>
		/// job.json goes last. It is what marks the folder as ours, so a delete stopped half-way by a
		/// locked archive (someone still copying it over UNC) must leave it in place - otherwise the
		/// rest of the folder would never be recognised, and never cleaned, again.
		/// </remarks>
		static bool TryDeleteFolder( string folder )
		{
			try
			{
				if( !Directory.Exists( folder ) ) return true;

				var dir = new DirectoryInfo( folder );
				foreach( var sub in dir.GetDirectories() )
					sub.Delete( true );
				foreach( var file in dir.GetFiles() )
					if( !string.Equals( file.Name, JobFileName, StringComparison.OrdinalIgnoreCase ) )
						file.Delete();

				Directory.Delete( folder, true );
				return true;
			}
			catch( Exception e )
			{
				log.Warn( $"Could not delete '{folder}': {e.Message}" );
				return false;
			}
		}

		// ---- job.json -----------------------------------------------------------------------------

		static DownloadJobRecord? ReadRecord( string folder )
		{
			try
			{
				var path = Path.Combine( folder, JobFileName );
				if( !File.Exists( path ) ) return null;
				return JsonSerializer.Deserialize<DownloadJobRecord>( File.ReadAllText( path ), _readOptions );
			}
			catch( Exception e )
			{
				log.Warn( $"Could not read the job file in '{folder}': {e.Message}" );
				return null;
			}
		}

		/// <summary>Writes through a temporary name, so that a job.json found on disk is always a whole one.</summary>
		static void WriteRecord( string folder, DownloadJobRecord record )
		{
			Directory.CreateDirectory( folder );
			var path = Path.Combine( folder, JobFileName );
			var temp = path + ".writing";
			File.WriteAllText( temp, AppFilesResult.ToJson( record ) );
			File.Move( temp, path, true );
		}

		public static DownloadRequest? ParseRequest( string json )
			=> JsonSerializer.Deserialize<DownloadRequest>( json, _readOptions );
	}
}
