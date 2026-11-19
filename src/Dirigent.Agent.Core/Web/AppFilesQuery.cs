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
	}

	/// <summary>One top-level VFS node of an application, resolved.</summary>
	public class AppFileNodeInfo
	{
		public string id { get; set; } = string.Empty;
		public string title { get; set; } = string.Empty;
		public string type { get; set; } = string.Empty;
		public string? machine { get; set; }
		public string? localPath { get; set; }
		public string? uncPath { get; set; }
		public string? uncError { get; set; }
		public string? error { get; set; }
		public List<string> notes { get; set; } = new();
		public List<AppFileInfo> files { get; set; } = new();

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
		public List<AppFileNodeInfo> nodes { get; set; } = new();

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

		AppFilesQuery( Master master, string app, string? plan, string planSource, List<VfsNodeDef> nodes )
		{
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

			return new AppFilesQuery( master, appId.ToString(), plan, source, def.VfsNodes.ToList() );
		}

		/// <summary>
		/// Resolves every node of the app, all at once. A node that cannot be resolved carries its
		/// own error and does not take the others with it. Not for the master thread.
		/// </summary>
		public async Task<AppFilesResult> ResolveAsync( bool includeContent )
		{
			var nodes = await Task.WhenAll( _nodes.Select( n => ResolveNodeAsync( n, includeContent ) ) );

			return new AppFilesResult()
			{
				app = _app,
				plan = _plan,
				planSource = _planSource,
				nodes = nodes.ToList(),
			};
		}

		Task<AppFileNodeInfo> ResolveNodeAsync( VfsNodeDef def, bool includeContent )
			=> ResolveNodeEntryAsync( _master, def, includeContent );

		/// <summary>
		/// Resolves one node and describes it: the one implementation of a node entry, shared by the
		/// application query and the node query. A failure is reported in the entry, never thrown.
		/// </summary>
		public static async Task<AppFileNodeInfo> ResolveNodeEntryAsync( Master master, VfsNodeDef def, bool includeContent )
		{
			var info = new AppFileNodeInfo()
			{
				id = def.Id,
				title = !string.IsNullOrEmpty( def.Title ) ? def.Title : def.Id,
				type = TypeName( def ),
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
				CollectFiles( master, root, info.title, info.files, isRoot: true );
			}
			catch( Exception e )
			{
				info.error = Tools.JustFirstLine( e.Message );
			}

			return info;
		}

		static string? NullIfEmpty( string? s ) => string.IsNullOrEmpty( s ) ? null : s;

		static string TypeName( VfsNodeDef def ) => def switch
		{
			FileDef => "File",
			FolderDef => "Folder",
			VFolderDef => "VFolder",
			FilePackageDef => "FilePackage",
			FileRef => "FileRef",
			_ => def.GetType().Name,
		};

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

		static void CollectFiles( Master master, VfsNodeDef n, string virtualPath, List<AppFileInfo> files, bool isRoot )
		{
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
				} );
				return;
			}

			foreach( var c in n.Children )
			{
				var title = !string.IsNullOrEmpty( c.Title ) ? c.Title : c.Id;
				CollectFiles( master, c, virtualPath + "/" + title, files, isRoot: false );
			}
		}
	}

	public class NodeFilesResult
	{
		public string id { get; set; } = string.Empty;
		public string? machine { get; set; }
		public string? app { get; set; }
		public List<AppFileNodeInfo> nodes { get; set; } = new();

		/// <summary>The one-line JSON both the REST endpoint and the CLI command answer with.</summary>
		public string ToJson() => AppFilesResult.ToJson( this );
	}

	/// <summary>
	/// The files of any top-level VFS node - a file package, usually - addressed the way scripts
	/// address nodes. The whole selection is resolved as one reference, so the resolver batches it into
	/// one round trip per machine and turns an unreachable member into a note on its container.
	/// </summary>
	public class NodeFilesQuery
	{
		readonly Master _master;
		readonly Scripts.BuiltIn.VfsNodeSelector _selector;

		public NodeFilesQuery( Master master, string id, string? machine, string? app )
		{
			_master = master;
			_selector = new Scripts.BuiltIn.VfsNodeSelector() { Id = id, MachineId = machine, AppId = app };
			_selector.ToFileRef(); // validates the id
		}

		/// <exception cref="AppFilesNotFoundException">nothing matches the selector</exception>
		public async Task<NodeFilesResult> ResolveAsync( bool includeContent )
		{
			var entry = await AppFilesQuery.ResolveNodeEntryAsync( _master, _selector.ToFileRef(), includeContent );

			if( entry.resolvedToNothing )
				throw new AppFilesNotFoundException( $"No node matches {_selector}." );

			return new NodeFilesResult()
			{
				id = _selector.Id!,
				machine = string.IsNullOrEmpty( _selector.MachineId ) ? null : _selector.MachineId,
				app = string.IsNullOrEmpty( _selector.AppId ) ? null : _selector.AppId,
				nodes = new List<AppFileNodeInfo>() { entry },
			};
		}
	}
}
