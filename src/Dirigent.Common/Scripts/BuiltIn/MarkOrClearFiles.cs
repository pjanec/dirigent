using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dirigent;

namespace Dirigent.Scripts.BuiltIn
{
	/*
	* Draws a line under a package of files, or empties it, so that the next download delivers one
	* test run instead of the whole afternoon.
	*
	* The three operations - Clear, Mark, Unmark - differ only in what the per-machine slave does to
	* each file, so they share everything down here: finding the files, deciding which machines hold
	* them, following the slaves, and reporting what happened. See MarkFilesSlave for the file end of
	* it, and docs/MarkAndClear.md for why marking rather than deleting.
	*/
	public abstract class MarkOrClearFiles : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		//[MessagePack.MessagePackObject]
		public class TArgs : ScriptActionArgs
		{
			/// <summary>
			/// What to act on, named by its config id. Used when VfsNode is not given, which is the
			/// case for every caller but a GUI.
			/// </summary>
			public VfsNodeSelector? Node;
		}

		//[MessagePack.MessagePackObject]
		public class TResult
		{
			public int Cleared;
			public int Marked;
			public int Unmarked;
			public int Skipped;
			public int Absent;
			public int Failed;

			/// <summary>The machines that took part.</summary>
			public List<string> Machines = new();

			/// <summary>What went wrong. Empty on a clean run.</summary>
			public List<string> Errors = new();
		}

		class SlaveTask
		{
			public string MachineName = "";
			public Guid ScriptId;
			public Task<MarkFilesSlave.TResult>? Task;
			public MarkFilesSlave.TResult? Result;
		}

		/// <summary>Which of the three this is.</summary>
		protected abstract MarkFilesSlave.EOperation Operation { get; }

		/// <summary>What the operation is called in the status line and in the closing message.</summary>
		protected abstract string Verb { get; }

		/// <summary>How much of the bar the lookup gets - the machines do the rest.</summary>
		const double _resolvedAt = 0.05;

		/// <summary>How often the machines are asked how far they have got.</summary>
		const int _pollPeriodMs = 500;

		readonly List<SlaveTask> _slaveTasks = new();

		protected async override Task<string?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args );
			if( args is null ) throw new NullReferenceException( "Args is null" );

			var result = new TResult();

			try
			{
				await SetStatus( "Looking up the files...", null, 0.0 );

				var (found, title) = await FindFiles( args );

				// what the action asked to narrow the operation down to, if anything
				var container = Narrow( found, args.Args );

				var allMachines = new HashSet<string>();
				CollectMachines( container, allMachines );

				var clientStates = ( await Dirig.GetAllClientsStateAsync() ).ToDictionary( x => x.Key, y => y.Value );
				var onlineMachines = ( from x in allMachines
									   where clientStates.ContainsKey( x ) && clientStates[x].Connected
									   select x ).ToList();

				if( onlineMachines.Count == 0 )
				{
					// nothing was done, and saying so is the whole point: somebody about to run a test
					// must not believe the logs were cleared when they were not
					throw new Exception(
						$"None of the machines holding the files of '{title}' is online." );
				}

				await SetStatus( $"{Verb} on {onlineMachines.Count} machine(s)...", null, _resolvedAt );

				bool globalsAssigned = false;
				foreach( var mach in onlineMachines )
				{
					var slaveArgs = new MarkFilesSlave.TArgs()
					{
						Container = container,
						Operation = Operation,
						IncludeGlobals = !globalsAssigned, // the first machine takes the machine-less files
					};
					globalsAssigned = true;

					var task = Dirig.RunScriptAsync<MarkFilesSlave.TArgs, MarkFilesSlave.TResult>(
						mach, MarkFilesSlave._Name, null, slaveArgs, $"{Verb} on {mach}", out var inst );

					_slaveTasks.Add( new SlaveTask() { MachineName = mach, ScriptId = inst, Task = task! } );
				}

				var results = await FollowSlaves();
				for( int i = 0; i < results.Length; i++ ) _slaveTasks[i].Result = results[i];

				Summarize( result );

				await SetStatus( Report( result, oneLine: true ), null, 1.0 );

				await Dirig.SendAsync( new Net.UserNotificationMessage
				{
					HostClientId = Requestor,
					Category = result.Failed > 0
								? Net.UserNotificationMessage.ECategory.Warning
								: Net.UserNotificationMessage.ECategory.Info,
					PresentationType = Net.UserNotificationMessage.EPresentationType.MessageBox,

					// a message box rather than a balloon, because this is one half of a two-step
					// procedure: the operator has to know the first step is over before running the test
					Message = $"{title}\n\n{Report( result, oneLine: false )}",
				} );
			}
			catch( OperationCanceledException )
			{
				await SetStatus( "Cancelling...", null, null );
				await CancelSlaves();
				throw;
			}
			catch( Exception e )
			{
				await Dirig.SendAsync( new Net.UserNotificationMessage
				{
					HostClientId = Requestor,
					Category = Net.UserNotificationMessage.ECategory.Error,
					PresentationType = Net.UserNotificationMessage.EPresentationType.MessageBox,
					Message = $"{Verb} failed!\n\n{e.Message}",
				} );

				result.Errors.Add( e.Message );
				throw;
			}

			return Tools.Serialize( result );
		}

		/// <summary>
		/// The resolved tree to act on. Three ways in, the same as the download has: a caller naming
		/// the node by id, a GUI handing over the definition to be resolved, or an already resolved
		/// tree.
		/// </summary>
		/// <remarks>
		/// Resolved with the content, because a &lt;Folder&gt; node is a rule rather than a list and
		/// only the resolution turns it into the files that are there now.
		/// </remarks>
		async Task<(VfsNodeDef Container, string Title)> FindFiles( TArgs args )
		{
			var vfsNode = args.VfsNode;

			if( vfsNode is null )
			{
				if( args.Node is null )
					throw new ArgumentException( "Neither VfsNode nor Node given - nothing to act on." );

				vfsNode = await Dirig.ResolveAsync( args.Node.ToFileRef(), false, true );
				if( vfsNode is null )
					throw new Exception( $"No VFS node matching {args.Node}." );
			}
			else if( args.VfsNodeNeedsResolving )
			{
				vfsNode = await Dirig.ResolveAsync( vfsNode, false, true );
				if( vfsNode is null )
					throw new Exception( $"Nothing found for {args.VfsNode!.Id ?? args.VfsNode.Title}." );
			}

			var title = vfsNode.Title;
			if( string.IsNullOrEmpty( title ) ) title = Path.GetFileName( vfsNode.Path ?? "" );
			if( string.IsNullOrEmpty( title ) ) title = vfsNode.Id;
			if( string.IsNullOrEmpty( title ) ) title = "file";

			// a single file - a Files tab row, typically - is wrapped so that everything below works
			// on children only
			if( vfsNode.IsContainer )
				return ( vfsNode, title );

			return ( new VFolderDef() { Title = title, Children = new List<VfsNodeDef>() { vfsNode } }, title );
		}

		/// <summary>
		/// Keeps only the children whose id matches one of the given patterns.
		/// </summary>
		/// <remarks>
		/// The patterns are the script's own argument - a semicolon separated list, the same wildcards
		/// &lt;FileRef&gt; uses - so that one package can carry a "Clear" that touches the logs and
		/// leaves the rest alone. Empty means everything in scope, which is the usual case.
		///
		/// Matched against the direct children only: those are the entries the configuration named and
		/// gave ids to. What a matched entry expands into comes along with it.
		/// </remarks>
		static VfsNodeDef Narrow( VfsNodeDef container, string? patterns )
		{
			if( string.IsNullOrWhiteSpace( patterns ) ) return container;

			var wanted = patterns!.Split( new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries )
								.Select( x => x.Trim() )
								.Where( x => x.Length > 0 )
								.ToList();

			if( wanted.Count == 0 ) return container;

			bool Matches( VfsNodeDef node )
				=> !string.IsNullOrEmpty( node.Id )
				&& wanted.Any( p => FileSystemName.MatchesSimpleExpression( p, node.Id ) );

			var kept = container.Children.Where( Matches ).ToList();

			if( kept.Count == 0 )
				throw new Exception( $"Nothing in '{container.Title}' matches '{patterns}'." );

			return new VFolderDef()
			{
				Id = container.Id,
				Title = container.Title,
				Children = kept,
			};
		}

		/// <summary>
		/// Waits for the machines, counting them off as they finish.
		/// </summary>
		/// <remarks>
		/// Per machine, not per byte: these operations read lengths and delete files, so the machines
		/// are done in about the same time and weighing them by size would buy nothing.
		/// </remarks>
		async Task<MarkFilesSlave.TResult[]> FollowSlaves()
		{
			var all = Task.WhenAll( from x in _slaveTasks select x.Task );

			while( true )
			{
				var finished = await Task.WhenAny( all, Task.Delay( _pollPeriodMs, CancellationToken ) );
				if( finished == all ) break;

				// WhenAny hands back the cancelled delay rather than throwing
				CancellationToken.ThrowIfCancellationRequested();

				var done = _slaveTasks.Count( x => x.Task?.IsCompleted ?? false );
				var fraction = _slaveTasks.Count > 0 ? (double) done / _slaveTasks.Count : 1.0;

				await SetStatus( $"{Verb}: {done} of {_slaveTasks.Count} machine(s) done",
								null, _resolvedAt + ( 1.0 - _resolvedAt ) * fraction );
			}

			return await all;
		}

		async Task CancelSlaves()
		{
			foreach( var st in _slaveTasks )
			{
				try { await Dirig.SendAsync( new Net.KillScriptMessage( Requestor, st.ScriptId ) ); }
				catch( Exception e ) { log.Warn( $"Could not stop {Verb} on {st.MachineName}: {e.Message}" ); }
			}
		}

		void Summarize( TResult result )
		{
			foreach( var st in _slaveTasks )
			{
				result.Machines.Add( st.MachineName );

				var r = st.Result;
				if( r is null )
				{
					result.Failed++;
					result.Errors.Add( $"{st.MachineName}: no answer." );
					continue;
				}

				result.Cleared += r.Cleared;
				result.Marked += r.Marked;
				result.Unmarked += r.Unmarked;
				result.Skipped += r.Skipped;
				result.Absent += r.Absent;
				result.Failed += r.Failed;

				foreach( var e in r.Exceptions )
					result.Errors.Add( $"{st.MachineName}: {e.Message}" );
			}
		}

		/// <summary>
		/// What happened, per machine, as the operator wants to read it.
		/// </summary>
		string Report( TResult result, bool oneLine )
		{
			var perMachine = _slaveTasks
					.OrderBy( st => st.MachineName, StringComparer.OrdinalIgnoreCase )
					.Select( st => $"{st.MachineName}: {( st.Result is null ? "no answer" : st.Result.Summary() )}" )
					.ToList();

			if( oneLine )
				return $"{Verb} done - " + string.Join( "; ", perMachine );

			var text = new StringBuilder();
			text.AppendLine( $"{Verb} finished." );
			text.AppendLine();
			foreach( var line in perMachine ) text.AppendLine( $"    {line}" );

			// The one thing worth explaining, because it is almost always a forgotten attribute rather
			// than a decision - and it would otherwise show up as a log that mysteriously kept its
			// old contents.
			if( result.Skipped > 0 )
			{
				text.AppendLine();
				text.AppendLine( $"{result.Skipped} file(s) were left alone: their configuration does not"
								+ " say Clearable=\"1\". Configuration files are meant to stay that way;"
								+ " a log that should have been cleared needs the attribute." );
			}

			if( result.Marked > 0 && Operation == MarkFilesSlave.EOperation.Clear )
			{
				text.AppendLine();
				text.AppendLine( $"{result.Marked} file(s) are in use and could not be emptied. A line was"
								+ " drawn under them instead, so the next download takes only what is"
								+ " written from now on." );
			}

			if( result.Errors.Count > 0 )
			{
				text.AppendLine();
				text.AppendLine( "Problems:" );
				foreach( var e in result.Errors ) text.AppendLine( $"    {e}" );
			}

			return text.ToString();
		}

		static void CollectMachines( VfsNodeDef container, HashSet<string> allMachines )
		{
			foreach( var child in container.Children )
			{
				if( !string.IsNullOrEmpty( child.MachineId ) )
					allMachines.Add( child.MachineId );

				CollectMachines( child, allMachines );
			}
		}
	}


	/// <summary>
	/// Empties what can be emptied and marks the rest - the one click before a test run.
	/// </summary>
	public class ClearFiles : MarkOrClearFiles
	{
		public static readonly string _Name = "BuiltIns/ClearFiles.cs";

		protected override MarkFilesSlave.EOperation Operation => MarkFilesSlave.EOperation.Clear;
		protected override string Verb => "Clear";
	}


	/// <summary>
	/// Draws a line under the files, destroying nothing - the same thing on a production site.
	/// </summary>
	public class MarkFiles : MarkOrClearFiles
	{
		public static readonly string _Name = "BuiltIns/MarkFiles.cs";

		protected override MarkFilesSlave.EOperation Operation => MarkFilesSlave.EOperation.Mark;
		protected override string Verb => "Mark";
	}


	/// <summary>
	/// Forgets the marks, so that the next download takes the whole files again.
	/// </summary>
	public class UnmarkFiles : MarkOrClearFiles
	{
		public static readonly string _Name = "BuiltIns/UnmarkFiles.cs";

		protected override MarkFilesSlave.EOperation Operation => MarkFilesSlave.EOperation.Unmark;
		protected override string Verb => "Unmark";
	}
}
