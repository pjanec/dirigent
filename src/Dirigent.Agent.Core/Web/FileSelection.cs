using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dirigent.Web
{
	/// <summary>
	/// A selection of files as a tool sends it: file definitions (each with optional machine and application filters,
	/// wildcards in all three) and applications. The same object previews, downloads and marks.
	/// </summary>
	public class FileSelectionRequest
	{
		/// <summary>The file definitions, packages among them.</summary>
		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		public List<DownloadFileDefSelection>? fileDefs { get; set; }

		/// <summary>The applications: "m1.a" or "m1.a@plan".</summary>
		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		public List<string>? apps { get; set; }
	}

	/// <summary>The answer of POST /api/files/query: the preview of each item, and the totals of all of them.</summary>
	public class FilesQueryResult
	{
		/// <summary>One per selected item, in the order of the selection - a <see cref="FileDefFilesResult"/> for a file definition, an <see cref="AppFilesResult"/> for an application.</summary>
		public List<object> items { get; set; } = new();

		/// <summary>The sum of the items' totals. A file reached through two items counts twice, as it is in two folders of a download.</summary>
		public FilesTotals totals { get; set; } = new();

		public string ToJson() => AppFilesResult.ToJson( this );
	}

	/// <summary>
	/// The one parser and resolver of a selection, used by the preview, the download and the marking. All the items are
	/// looked up - and the unknown one named - before anything runs.
	/// </summary>
	public class FileSelection
	{
		public class Item
		{
			public bool IsApp { get; init; }

			/// <summary>How the item is named in messages and echoes: the id, with its filters; the app.</summary>
			public string Label { get; init; } = string.Empty;

			public DownloadFileDefSelection? FileDef { get; init; }
			public string? App { get; init; }

			public FileDefFilesQuery? FileDefQuery { get; init; }
			public AppFilesQuery? AppQuery { get; init; }
		}

		public List<Item> Items { get; } = new();

		public int Count => Items.Count;

		/// <summary>The items as a person reads them: "a; b machine=m1; m1.x".</summary>
		public string Describe() => string.Join( "; ", Items.Select( i => i.Label ) );

		static string LabelOf( DownloadFileDefSelection f )
			=> f.id + ( string.IsNullOrEmpty( f.machine ) ? "" : $" machine={f.machine}" ) + ( string.IsNullOrEmpty( f.app ) ? "" : $" app={f.app}" );

		public static FileSelectionRequest? ParseJson( string json )
			=> JsonSerializer.Deserialize<FileSelectionRequest>( json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );

		/// <summary>
		/// Looks every item up. Master thread only.
		/// </summary>
		/// <param name="single">the older single forms of a download request, which count as items of their own, first</param>
		/// <exception cref="ArgumentException">nothing is selected, or an item has no id</exception>
		/// <exception cref="AppFilesNotFoundException">an item matches nothing; the message names it</exception>
		public static FileSelection Parse( Master master, FileSelectionRequest? request,
				DownloadFileDefSelection? singleFileDef = null, string? singleApp = null )
		{
			var fileDefs = new List<DownloadFileDefSelection>();
			if( singleFileDef is not null && !string.IsNullOrEmpty( singleFileDef.id ) ) fileDefs.Add( singleFileDef );
			if( request?.fileDefs is not null ) fileDefs.AddRange( request.fileDefs );

			var apps = new List<string>();
			if( !string.IsNullOrEmpty( singleApp ) ) apps.Add( singleApp );
			if( request?.apps is not null ) apps.AddRange( request.apps );

			if( fileDefs.Count == 0 && apps.Count == 0 )
				throw new ArgumentException( "Nothing is selected: 'fileDefs' and 'apps' are both empty." );

			var sel = new FileSelection();

			foreach( var f in fileDefs )
			{
				if( f is null || string.IsNullOrEmpty( f.id ) )
					throw new ArgumentException( "A file definition of the selection has no 'id'." );

				sel.Items.Add( new Item()
				{
					Label = LabelOf( f ),
					FileDef = f,
					FileDefQuery = FileDefFilesQuery.Prepare( master, f.id!, f.machine, f.app ),
				} );
			}

			foreach( var a in apps )
			{
				if( string.IsNullOrEmpty( a ) )
					throw new ArgumentException( "An application of the selection is empty." );

				sel.Items.Add( new Item()
				{
					IsApp = true,
					Label = a,
					App = a,
					AppQuery = AppFilesQuery.Prepare( master, a ),
				} );
			}

			return sel;
		}

		/// <summary>The file definitions of the selection, each once, in order - for the marking, which is per path anyway.</summary>
		public List<VfsNodeDef> UnionOfDefinitions()
		{
			var defs = new List<VfsNodeDef>();
			foreach( var item in Items )
			{
				var of = item.IsApp ? item.AppQuery!.Nodes : item.FileDefQuery!.Defs;
				foreach( var d in of )
					if( !defs.Any( x => x.Guid == d.Guid ) ) defs.Add( d );
			}
			return defs;
		}

		/// <summary>Previews every item, all at once. Not for the master thread.</summary>
		public async Task<FilesQueryResult> ResolveAsync( bool includeContent )
		{
			var results = await Task.WhenAll( Items.Select( async i =>
				i.IsApp ? (object) await i.AppQuery!.ResolveAsync( includeContent )
						: await i.FileDefQuery!.ResolveAsync( includeContent ) ) );

			return new FilesQueryResult()
			{
				items = results.ToList(),
				totals = FilesTotals.Sum( results.Select( r => r is AppFilesResult a ? a.totals : ( (FileDefFilesResult) r ).totals ) ),
			};
		}
	}
}
