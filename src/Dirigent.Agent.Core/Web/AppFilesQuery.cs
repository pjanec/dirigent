using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dirigent.Web
{
	/// <summary>One file of an application, as a diagnostic tool wants to find it.</summary>
	public class AppFileInfo
	{
		/// <summary>Titles from the node down to the file, joined with '/'.</summary>
		public string path { get; set; } = string.Empty;
		public string? machine { get; set; }
		public string? localPath { get; set; }
		public string? uncPath { get; set; }

		/// <summary>Bytes, as found when the file was looked up; null where it could not be read.</summary>
		public long? size { get; set; }

		/// <summary>Last write time, UTC.</summary>
		public DateTime? modified { get; set; }

		/// <summary>When a line was drawn under the file by Mark or Clear (UTC); null if none.</summary>
		public DateTime? markedAt { get; set; }

		/// <summary>The length of the file when it was marked - where a download from the mark starts.</summary>
		public long? markedAtBytes { get; set; }

		/// <summary>
		/// Set when the file definition limits a download to the end of the file (<c>TailBytes</c>) and
		/// the file is bigger: how many bytes at most a download will hold.
		/// </summary>
		public long? truncatedTo { get; set; }

		/// <summary>The application the file belongs to, where it belongs to one.</summary>
		public string? app { get; set; }

		/// <summary>The id of the file definition the file came from: the one a package or an application referred to.</summary>
		public string? fileDefId { get; set; }

		/// <summary>
		/// The software component the file belongs to: its application's <c>Component</c>, else that of the nearest package or
		/// file definition around it that names one; null if none does.
		/// </summary>
		public string? component { get; set; }
	}

	/// <summary>How much a preview holds; the same three numbers wherever a preview is answered.</summary>
	public class FilesTotals
	{
		public int files { get; set; }

		/// <summary>The sum of the sizes of the files, as they are on disk.</summary>
		public long bytes { get; set; }

		/// <summary>What a download of these files would hold: from the mark on, and no more than the TailBytes of the definition.</summary>
		public long bytesToCollect { get; set; }

		/// <summary>
		/// What a download takes of one file: <c>min( size - markedAtBytes, truncatedTo )</c>, never below zero. A file
		/// of unknown size counts as nothing.
		/// </summary>
		public static long BytesToCollect( long? size, long? markedAtBytes, long? truncatedTo )
		{
			var s = size ?? 0;
			return Math.Max( 0, Math.Min( s - ( markedAtBytes ?? 0 ), truncatedTo ?? s ) );
		}

		public static FilesTotals Of( IEnumerable<AppFileInfo> files )
		{
			var t = new FilesTotals();
			foreach( var f in files )
			{
				t.files++;
				t.bytes += f.size ?? 0;
				t.bytesToCollect += BytesToCollect( f.size, f.markedAtBytes, f.truncatedTo );
			}
			return t;
		}

		/// <summary>The sum of several; nothing is left out for being in two of them - a download holds each copy.</summary>
		public static FilesTotals Sum( IEnumerable<FilesTotals> parts )
		{
			var t = new FilesTotals();
			foreach( var p in parts )
			{
				t.files += p.files;
				t.bytes += p.bytes;
				t.bytesToCollect += p.bytesToCollect;
			}
			return t;
		}
	}

	/// <summary>One top-level file definition of an application or of the shared config, resolved.</summary>
	public class FileDefEntry
	{
		public string id { get; set; } = string.Empty;
		public string title { get; set; } = string.Empty;
		public string type { get; set; } = string.Empty;
		public string? description { get; set; }
		public string? machine { get; set; }
		public string? localPath { get; set; }
		public string? uncPath { get; set; }
		public string? uncError { get; set; }
		public string? error { get; set; }
		public List<string> notes { get; set; } = new();
		public List<AppFileInfo> files { get; set; } = new();
		public FilesTotals totals { get; set; } = new();

		/// <summary>The resolver found nothing at all for the node. Not part of the answer.</summary>
		[JsonIgnore]
		public bool resolvedToNothing { get; set; }
	}

	public class AppFilesResult
	{
		public string app { get; set; } = string.Empty;
		public string? plan { get; set; }
		/// <summary>"requested", "lastRun" or "none"</summary>
		public string planSource { get; set; } = "none";
		public List<FileDefEntry> fileDefs { get; set; } = new();
		public FilesTotals totals { get; set; } = new();

		static readonly JsonSerializerOptions _json = new()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			WriteIndented = false,
		};

		public static string ToJson<T>( T value ) => JsonSerializer.Serialize( value, _json );

		/// <summary>The one-line JSON both the REST endpoint and the CLI command answer with.</summary>
		public string ToJson() => ToJson( this );
	}

	/// <summary>The app or the plan asked about does not exist.</summary>
	public class AppFilesNotFoundException : Exception
	{
		public AppFilesNotFoundException( string message ) : base( message ) {}
	}

	/// <summary>
	/// The actual files of one application instance, each with its machine-local path and, where a
	/// share allows, a UNC path. Shared by the REST endpoint and the GetAppFiles CLI command.
	/// </summary>
	/// <remarks>
	/// Two steps, because they live on different threads: <see cref="Prepare"/> reads the master's
	/// state and so must run on the master thread; <see cref="ResolveAsync"/> awaits the agents and
	/// must not, or the master tick would wait for answers only the tick can deliver.
	/// </remarks>
	public class AppFilesQuery
	{
		readonly Master _master;
		readonly string _app;
		readonly string? _plan;
		readonly string _planSource;
		readonly List<VfsNodeDef> _nodes;

		/// <summary>The application as "machine.app".</summary>
		public string AppIdText => _app;

		/// <summary>The plan the definition was taken from; null for the default definition.</summary>
		public string? Plan => _plan;

		/// <summary>The top-level VFS nodes of the picked definition.</summary>
		public IReadOnlyList<VfsNodeDef> Nodes => _nodes;

		/// <summary>The definition picked: the requested plan's, the last-run plan's or the default one.</summary>
		public Dirigent.AppDef Def { get; }

		/// <summary>"requested", "lastRun" or "none".</summary>
		public string PlanSource => _planSource;

		AppFilesQuery( Master master, string app, string? plan, string planSource, List<VfsNodeDef> nodes, Dirigent.AppDef def )
		{
			Def = def;
			_master = master;
			_app = app;
			_plan = plan;
			_planSource = planSource;
			_nodes = nodes;
		}

		/// <summary>
		/// Picks the definition to answer from. Master thread only.
		/// </summary>
		/// <exception cref="AppFilesNotFoundException">unknown app or plan</exception>
		public static AppFilesQuery Prepare( Master master, string id )
		{
			var (appId, requestedPlan) = Tools.ParseAppIdWithPlan( id );

			if( string.IsNullOrEmpty( appId.AppId ) )
				throw new ArgumentSyntaxErrorException( "appIdTuple", id, "\"<machine>.<app>[@<plan>]\" expected" );

			Dirigent.AppDef? def;
			string? plan;
			string source;

			static Dirigent.AppDef? InPlan( Dirigent.PlanDef? plan, AppIdTuple appId )
				=> plan?.AppDefs.FirstOrDefault( a => a.Id.Equals( appId ) );

			if( !string.IsNullOrEmpty( requestedPlan ) )
			{
				def = InPlan( master.GetPlanDef( requestedPlan ), appId );
				if( def is null )
					throw new AppFilesNotFoundException( $"App {appId} not found in plan {requestedPlan}." );
				plan = requestedPlan;
				source = "requested";
			}
			else
			{
				var lastPlan = master.GetAppState( appId )?.PlanName;
				def = string.IsNullOrEmpty( lastPlan ) ? null : InPlan( master.GetPlanDef( lastPlan ), appId );

				if( def is not null )
				{
					plan = lastPlan;
					source = "lastRun";
				}
				else
				{
					def = master.GetAppDef( appId );
					if( def is null )
						throw new AppFilesNotFoundException( $"App {appId} not found." );
					plan = null;
					source = "none";
				}
			}

			return new AppFilesQuery( master, appId.ToString(), plan, source, def.VfsNodes.ToList(), def );
		}

		/// <summary>
		/// Resolves every node of the app, all at once. A node that cannot be resolved carries its
		/// own error and does not take the others with it. Not for the master thread.
		/// </summary>
		public async Task<AppFilesResult> ResolveAsync( bool includeContent )
		{
			var nodes = await Task.WhenAll( _nodes.Select( n => ResolveFileDefAsync( n, includeContent ) ) );

			return new AppFilesResult()
			{
				app = _app,
				plan = _plan,
				planSource = _planSource,
				fileDefs = nodes.ToList(),
				totals = FilesTotals.Sum( nodes.Select( n => n.totals ) ),
			};
		}

		Task<FileDefEntry> ResolveFileDefAsync( VfsNodeDef def, bool includeContent )
			=> ResolveFileDefEntryAsync( _master, def, includeContent, Def.Id.AppId );

		/// <summary>
		/// Resolves one node and describes it: the one implementation of a node entry, shared by the
		/// application query and the node query. A failure is reported in the entry, never thrown.
		/// </summary>
		public static async Task<FileDefEntry> ResolveFileDefEntryAsync( Master master, VfsNodeDef def, bool includeContent, string? defaultApp = null )
		{
			var info = new FileDefEntry()
			{
				id = def.Id,
				title = !string.IsNullOrEmpty( def.Title ) ? def.Title : def.Id,
				type = TypeName( def ),
				description = NullIfEmpty( def.Description ),
				machine = NullIfEmpty( def.MachineId ),
			};

			try
			{
				var root = await master.ResolveAsync( def, forceUNC: false, includeContent );

				if( root is null )
				{
					info.resolvedToNothing = true;
					info.notes.Add( "Nothing was found for this node." );
					return info;
				}

				if( string.IsNullOrEmpty( def.Title ) && !string.IsNullOrEmpty( root.Title ) )
					info.title = root.Title;

				info.machine = NullIfEmpty( root.MachineId );
				info.localPath = LocalPath( root );
				info.uncPath = TryUnc( master, root, out var uncError );
				info.uncError = uncError;

				CollectNotes( root, info.notes );
				CollectFiles( master, root, info.title, info.files, isRoot: true, def.Id, defaultApp );
				info.totals = FilesTotals.Of( info.files );
			}
			catch( Exception e )
			{
				info.error = Tools.JustFirstLine( e.Message );
			}

			return info;
		}

		static string? NullIfEmpty( string? s ) => string.IsNullOrEmpty( s ) ? null : s;

		public static string TypeName( VfsNodeDef def ) => Scripts.BuiltIn.ListVfsNodes.TypeName( def );

		/// <summary>The path on the machine owning the node; none for a global node, whose path is UNC already.</summary>
		static string? LocalPath( VfsNodeDef n )
			=> string.IsNullOrEmpty( n.Path ) || string.IsNullOrEmpty( n.MachineId ) ? null : n.Path;

		/// <summary>UNC path of a node; null with the reason in <paramref name="error"/> if no share covers it.</summary>
		static string? TryUnc( Master master, VfsNodeDef n, out string? error )
		{
			error = null;
			if( string.IsNullOrEmpty( n.Path ) ) return null;
			if( string.IsNullOrEmpty( n.MachineId ) ) return n.Path; // global: UNC already

			try
			{
				return master.MakeUNC( n.Path, n.MachineId, $"{n.Title}" );
			}
			catch( Exception e )
			{
				error = Tools.JustFirstLine( e.Message );
				return null;
			}
		}

		static void CollectNotes( VfsNodeDef n, List<string> notes )
		{
			if( n.Notes is not null ) notes.AddRange( n.Notes );
			foreach( var c in n.Children ) CollectNotes( c, notes );
		}

		static void CollectFiles( Master master, VfsNodeDef n, string virtualPath, List<AppFileInfo> files, bool isRoot, string topId, string? defaultApp,
				string? outerComponent = null )
		{
			var component = NullIfEmpty( n.Component ) ?? outerComponent;

			if( !n.IsContainer && n.Children.Count == 0 )
			{
				if( string.IsNullOrEmpty( n.Path ) ) return;

				// below the node a file is named by its file name: a "newest" node gives every file the
				// node's own title. The reason for a missing UNC path is the node's to report, once.
				var name = isRoot ? virtualPath : virtualPath.Substring( 0, virtualPath.LastIndexOf( '/' ) + 1 ) + System.IO.Path.GetFileName( n.Path );

				files.Add( new AppFileInfo()
				{
					path = name,
					machine = NullIfEmpty( n.MachineId ),
					localPath = LocalPath( n ),
					uncPath = TryUnc( master, n, out _ ),
					size = n.Size,
					modified = n.Modified,
					markedAt = n.MarkedAt,
					markedAtBytes = n.MarkedAtBytes,
					truncatedTo = n.TailBytes > 0 && n.Size > n.TailBytes ? n.TailBytes : null,
					app = NullIfEmpty( n.AppId ) ?? NullIfEmpty( defaultApp ),
					fileDefId = NullIfEmpty( n.OriginId ) ?? NullIfEmpty( topId ),
					component = ComponentOfApp( master, n.MachineId, NullIfEmpty( n.AppId ) ?? NullIfEmpty( defaultApp ) ) ?? component,
				} );
				return;
			}

			foreach( var c in n.Children )
			{
				var title = !string.IsNullOrEmpty( c.Title ) ? c.Title : c.Id;
				CollectFiles( master, c, virtualPath + "/" + title, files, isRoot: false, topId, defaultApp, component );
			}
		}

		/// <summary>The Component of the application, null if it has none or there is no such application.</summary>
		static string? ComponentOfApp( Master master, string? machine, string? app )
		{
			if( string.IsNullOrEmpty( machine ) || string.IsNullOrEmpty( app ) ) return null;
			return NullIfEmpty( master.GetAppDef( new AppIdTuple( machine, app ) )?.Component );
		}
	}

	public class FileDefFilesResult
	{
		public string id { get; set; } = string.Empty;
		public string? machine { get; set; }
		public string? app { get; set; }
		public List<FileDefEntry> fileDefs { get; set; } = new();
		public FilesTotals totals { get; set; } = new();

		/// <summary>The one-line JSON both the REST endpoint and the CLI command answer with.</summary>
		public string ToJson() => AppFilesResult.ToJson( this );
	}

	/// <summary>
	/// The files of the top-level file definitions matching an id (and optionally a machine and an
	/// application), each definition resolved as itself: a file package answers as a file package, with
	/// its own title and description, and a wildcard matching two definitions answers with two entries.
	/// </summary>
	/// <remarks>
	/// Two steps like <see cref="AppFilesQuery"/>: <see cref="Prepare"/> reads the master's registry and
	/// runs on the master thread; <see cref="ResolveAsync"/> awaits the agents and does not.
	/// </remarks>
	public class FileDefFilesQuery
	{
		readonly Master _master;
		readonly Scripts.BuiltIn.VfsNodeSelector _selector;
		readonly List<VfsNodeDef> _defs;

		/// <summary>The definitions the selector matched.</summary>
		public IReadOnlyList<VfsNodeDef> Defs => _defs;

		FileDefFilesQuery( Master master, Scripts.BuiltIn.VfsNodeSelector selector, List<VfsNodeDef> defs )
		{
			_master = master;
			_selector = selector;
			_defs = defs;
		}

		/// <summary>The matching definitions, found on the master thread.</summary>
		/// <exception cref="AppFilesNotFoundException">nothing matches the selector</exception>
		public static FileDefFilesQuery Prepare( Master master, string id, string? machine, string? app )
		{
			var selector = new Scripts.BuiltIn.VfsNodeSelector() { Id = id, MachineId = machine, AppId = app };
			selector.ToFileRef(); // validates the id

			var defs = ( from n in master.GetAllVfsNodesDef()
						 where Scripts.BuiltIn.ListVfsNodes.Matches( n, selector )
						 orderby n.Id, n.MachineId, n.AppId
						 select n ).ToList();

			if( defs.Count == 0 )
				throw new AppFilesNotFoundException( $"No file definition matches {selector}." );

			return new FileDefFilesQuery( master, selector, defs );
		}

		public async Task<FileDefFilesResult> ResolveAsync( bool includeContent )
		{
			var entries = await Task.WhenAll( _defs.Select( d => AppFilesQuery.ResolveFileDefEntryAsync( _master, d, includeContent ) ) );

			return new FileDefFilesResult()
			{
				id = _selector.Id!,
				machine = string.IsNullOrEmpty( _selector.MachineId ) ? null : _selector.MachineId,
				app = string.IsNullOrEmpty( _selector.AppId ) ? null : _selector.AppId,
				fileDefs = entries.ToList(),
				totals = FilesTotals.Sum( entries.Select( e => e.totals ) ),
			};
		}
	}
}
