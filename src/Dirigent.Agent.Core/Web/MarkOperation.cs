using System;
using System.Collections.Generic;
using System.Linq;

using Dirigent.Scripts.BuiltIn;

namespace Dirigent.Web
{
	/// <summary>What one machine did to the files of a selection, file by file.</summary>
	public class MarkMachineReport
	{
		public string machine { get; set; } = string.Empty;
		public List<string> marked { get; set; } = new();
		public List<string> cleared { get; set; } = new();
		public List<string> unmarked { get; set; } = new();
		public List<string> skippedNotClearable { get; set; } = new();
		public List<string> missing { get; set; } = new();
		public List<MarkFailedFile> failed { get; set; } = new();
	}

	public class MarkFailedFile
	{
		public string path { get; set; } = string.Empty;
		public string error { get; set; } = string.Empty;
	}

	/// <summary>The answer of a mark, clear or unmark: what happened to which file, per machine.</summary>
	public class MarkReport
	{
		/// <summary>"mark", "clear" or "unmark"</summary>
		public string operation { get; set; } = string.Empty;

		/// <summary>What was acted on, as the caller named it.</summary>
		public string selection { get; set; } = string.Empty;

		public List<MarkMachineReport> machines { get; set; } = new();
		public List<string> errors { get; set; } = new();

		/// <summary>Set when there is something to explain: nothing in the selection is Clearable, or nothing was found.</summary>
		public string? message { get; set; }

		public string ToJson() => AppFilesResult.ToJson( this );
	}

	/// <summary>
	/// A mark, clear or unmark of the files of some file definitions or of an application: started on the master
	/// thread as the existing BuiltIns/MarkFiles.cs (ClearFiles, UnmarkFiles) script - the machinery the GUI uses -
	/// and reported from what the script returns.
	/// </summary>
	public class MarkOperation
	{
		public enum EKind { Mark, Clear, Unmark }

		public Guid ScriptId { get; }
		public EKind Kind { get; }
		public string Selection { get; }

		MarkOperation( Guid id, EKind kind, string selection )
		{
			ScriptId = id;
			Kind = kind;
			Selection = selection;
		}

		public static string Word( EKind kind ) => kind.ToString().ToLowerInvariant();

		static string ScriptOf( EKind kind ) => kind switch
		{
			EKind.Mark => MarkFiles._Name,
			EKind.Clear => ClearFiles._Name,
			_ => UnmarkFiles._Name,
		};

		/// <summary>The file definitions matching an id, narrowed by machine and app. Master thread only.</summary>
		/// <exception cref="AppFilesNotFoundException">nothing matches</exception>
		public static MarkOperation StartForFileDefs( Master master, EKind kind, string id, string? machine, string? app )
		{
			var query = FileDefFilesQuery.Prepare( master, id, machine, app );
			var selection = id + ( string.IsNullOrEmpty( machine ) ? "" : $" machine={machine}" ) + ( string.IsNullOrEmpty( app ) ? "" : $" app={app}" );
			return Start( master, kind, selection, id, query.Defs );
		}

		/// <summary>
		/// The file definitions matching any of the ids (wildcards allowed), each once. Null if none of them matches
		/// anything - the config said they would, so the caller says so. Master thread only.
		/// </summary>
		public static MarkOperation? StartForFileDefIds( Master master, EKind kind, IEnumerable<string> ids, string selection, bool planOwned = false )
		{
			var defs = new List<VfsNodeDef>();

			foreach( var id in ids )
			{
				try
				{
					foreach( var d in FileDefFilesQuery.Prepare( master, id, null, null ).Defs )
						if( !defs.Any( x => x.Guid == d.Guid ) ) defs.Add( d );
				}
				catch( AppFilesNotFoundException )
				{
				}
			}

			return defs.Count == 0 ? null : Start( master, kind, selection, selection, defs, planOwned );
		}

		/// <summary>
		/// One operation over the union of a selection of file definitions and applications: each file once, whatever
		/// number of items reach it. All the items were looked up when the selection was parsed. Master thread only.
		/// </summary>
		public static MarkOperation StartForSelection( Master master, EKind kind, FileSelection selection )
		{
			var text = selection.Describe();
			return Start( master, kind, text, text, selection.UnionOfDefinitions() );
		}

		/// <summary>All the file definitions of an application, picked as for the file previews. Master thread only.</summary>
		/// <exception cref="AppFilesNotFoundException">unknown app or plan</exception>
		public static MarkOperation StartForApp( Master master, EKind kind, string appId )
		{
			var query = AppFilesQuery.Prepare( master, appId );
			return Start( master, kind, appId, query.AppIdText, query.Nodes );
		}

		static MarkOperation Start( Master master, EKind kind, string selection, string title, IEnumerable<VfsNodeDef> defs, bool planOwned = false )
		{
			// the definitions go over as they are; the script resolves them itself, with progress
			var args = new MarkOrClearFiles.TArgs()
			{
				VfsNode = new VFolderDef()
				{
					Guid = Guid.NewGuid(),
					Id = title,
					Title = title,
					IsContainer = true,
					Children = defs.ToList(),
				},
				VfsNodeNeedsResolving = true,
				PlanOwned = planOwned,
			};

			var id = Guid.NewGuid();
			if( planOwned ) PlanOwnedScripts.Add( id );
			master.StartSingletonScript( "", id, ScriptOf( kind ), Tools.Serialize( args ) );
			return new MarkOperation( id, kind, selection );
		}

		/// <summary>The state of the script. Master thread only.</summary>
		public Dirigent.ScriptState? State( Master master ) => master.GetScriptState( "", ScriptId );

		/// <summary>The report of a script that has ended. Throws what went wrong if it did not finish.</summary>
		/// <exception cref="Exception">the script failed or was cancelled</exception>
		public MarkReport ReportOf( Dirigent.ScriptState state )
		{
			if( state.Status != Dirigent.EScriptStatus.Finished )
			{
				string why = $"The {Word( Kind )} ended as {state.Status}.";
				try
				{
					var ex = Tools.Deserialize<SerializedException>( state.Data );
					if( ex is not null && !string.IsNullOrEmpty( ex.Message ) ) why = Tools.JustFirstLine( ex.Message );
				}
				catch { }
				throw new Exception( why );
			}

			var result = Tools.Deserialize<MarkOrClearFiles.TResult>( state.Data ) ?? new MarkOrClearFiles.TResult();

			static List<string> Paths( IEnumerable<string> p ) => p.Distinct( StringComparer.OrdinalIgnoreCase ).ToList();

			var report = new MarkReport()
			{
				operation = Word( Kind ),
				selection = Selection,
				errors = result.Errors,
				machines = result.PerMachine.Select( m => new MarkMachineReport()
				{
					machine = m.Machine,
					marked = Paths( m.Marked ),
					cleared = Paths( m.Cleared ),
					unmarked = Paths( m.Unmarked ),
					skippedNotClearable = Paths( m.SkippedNotClearable ),
					missing = Paths( m.Missing ),
					failed = m.Failed.Select( f => new MarkFailedFile() { path = f.Path, error = f.Error } ).ToList(),
				} ).ToList(),
			};

			var done = report.machines.Sum( m => m.marked.Count + m.cleared.Count + m.unmarked.Count );
			var skipped = report.machines.Sum( m => m.skippedNotClearable.Count );

			if( Kind != EKind.Unmark && done == 0 && skipped > 0 )
				report.message = $"Nothing in the selection is Clearable: {skipped} file(s) were left alone. Only files whose definition says Clearable=\"1\" are touched.";
			else
			if( done == 0 && skipped == 0 && report.machines.Sum( m => m.missing.Count + m.failed.Count ) == 0 )
				report.message = Kind == EKind.Unmark ? "There was no mark to drop." : "The selection holds no files.";

			return report;
		}
	}
}
