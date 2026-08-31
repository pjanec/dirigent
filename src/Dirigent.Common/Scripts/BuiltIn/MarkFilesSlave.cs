using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dirigent;

namespace Dirigent.Scripts.BuiltIn
{
	/*
	* Draws a line under the files of this machine, or empties them, so that a later collection
	* delivers just one test run.
	*
	* Runs where the files are, because the marks are kept per machine and a file can only be opened
	* by the machine that owns it. Started by ClearFiles / MarkFiles / UnmarkFiles, one instance per
	* machine holding any of the files.
	*/
	public class MarkFilesSlave : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/MarkFilesSlave.cs";

		/// <summary>What to do to each file.</summary>
		public enum EOperation
		{
			/// <summary>Empty it if that is possible without corrupting anything, mark it otherwise.</summary>
			Clear = 0,

			/// <summary>Record its length, touching the file itself in no way.</summary>
			Mark = 1,

			/// <summary>Forget its mark, so that the next collection takes the whole file again.</summary>
			Unmark = 2,
		}

		//[MessagePack.MessagePackObject]
		public class TArgs
		{
			/// <summary>The resolved tree to act on; only the leaf nodes matter.</summary>
			public VfsNodeDef? Container;

			public EOperation Operation;

			/// <summary>
			/// Whether this machine also takes the files belonging to no machine - one of them has to.
			/// </summary>
			public bool IncludeGlobals;

			public override string ToString() => $"{Operation} {Container}";
		}

		//[MessagePack.MessagePackObject]
		public class TResult
		{
			/// <summary>Files emptied - deleted, or truncated where the deletion was refused.</summary>
			public int Cleared;

			/// <summary>Files a line was drawn under, either because that was asked for or because
			/// the file was in use and could not be emptied.</summary>
			public int Marked;

			/// <summary>Marks dropped.</summary>
			public int Unmarked;

			/// <summary>Files passed over because they are not <see cref="VfsNodeDef.Clearable"/>.</summary>
			public int Skipped;

			/// <summary>Files that are not there - normal for a log not yet written.</summary>
			public int Absent;

			/// <summary>Files that should have been touched and could not be.</summary>
			public int Failed;

			/// <summary>What happened per file, for whoever wants to know which ones.</summary>
			public List<string> Details = new();

			public List<SerializedException> Exceptions = new();

			/// <summary>The counts as one line, for a report naming several machines.</summary>
			public string Summary()
			{
				var parts = new List<string>();
				if( Cleared > 0 ) parts.Add( $"{Cleared} cleared" );
				if( Marked > 0 ) parts.Add( $"{Marked} marked" );
				if( Unmarked > 0 ) parts.Add( $"{Unmarked} unmarked" );
				if( Skipped > 0 ) parts.Add( $"{Skipped} not clearable" );
				if( Absent > 0 ) parts.Add( $"{Absent} not there" );
				if( Failed > 0 ) parts.Add( $"{Failed} failed" );
				return parts.Count > 0 ? string.Join( ", ", parts ) : "nothing to do";
			}
		}

		TArgs? _args;
		FileMarkStore? _marks;
		TResult _result = new();

		protected override Task<string?> Run()
		{
			_args = Tools.Deserialize<TArgs>( Args );
			if( _args is null ) throw new NullReferenceException( "Args == null" );
			if( _args.Container is null ) throw new ArgumentException( "No files given." );

			// only an agent keeps marks, and only an agent should ever be asked to do this
			_marks = MarkStore;
			if( _marks is null )
				throw new Exception( $"{Dirig.Name} keeps no file marks - this script has to run on an agent." );

			Visit( _args.Container );

			// once, at the end: the store is a single file, and a package of thirty logs would
			// otherwise rewrite it thirty times
			_marks.Save();

			SetStatus( _result.Summary(), null, 1.0 );

			return Task.FromResult( Tools.Serialize( _result ) )!;
		}

		void Visit( VfsNodeDef container )
		{
			foreach( var node in container.Children )
			{
				CancellationToken.ThrowIfCancellationRequested();

				if( node.IsContainer )
				{
					Visit( node );
					continue;
				}

				if( !IsOurs( node ) ) continue;

				try
				{
					Act( node );
				}
				catch( OperationCanceledException )
				{
					throw; // the user asked us to stop; not a per-file problem
				}
				catch( Exception e )
				{
					_result.Failed++;
					_result.Details.Add( $"{node.Path}: {e.Message}" );
					_result.Exceptions.Add( new SerializedException( e ) );
				}
			}
		}

		bool IsOurs( VfsNodeDef node )
			=> node.MachineId == Dirig.Name
			|| ( string.IsNullOrEmpty( node.MachineId ) && _args!.IncludeGlobals );

		void Act( VfsNodeDef node )
		{
			var path = node.Path;
			if( string.IsNullOrEmpty( path ) )
			{
				_result.Failed++;
				_result.Details.Add( $"Node '{node.Id}' resolved to no path." );
				return;
			}

			// Unmark is the exception to the permission below, and deliberately so: it only ever
			// removes a mark, which can only make a later collection more complete. Refusing it on a
			// node whose Clearable was taken away after it had been marked would leave that mark in
			// place with no way of getting rid of it.
			if( _args!.Operation == EOperation.Unmark )
			{
				if( _marks!.Unmark( path! ) )
				{
					_result.Unmarked++;
					_result.Details.Add( $"Unmarked {path}" );
				}
				return;
			}

			// the whole safety of this feature, in one line: a file the configuration did not open up
			// is not touched, whatever was clicked
			if( !node.Clearable )
			{
				_result.Skipped++;
				_result.Details.Add( $"Not clearable, left alone: {path}" );
				return;
			}

			if( !File.Exists( path ) )
			{
				// a log not written yet needs nothing done to it: with no mark, the collection takes
				// it whole, which is exactly what came out of this run
				_result.Absent++;
				_result.Details.Add( $"Not there: {path}" );
				return;
			}

			SetStatus( Path.GetFileName( path ), null, null );

			if( _args.Operation == EOperation.Clear )
			{
				Clear( path! );
				return;
			}

			MarkOnly( path!, "Marked" );
		}

		/// <summary>
		/// Empties the file if nobody is holding it, marks it if somebody is.
		/// </summary>
		/// <remarks>
		/// The decision is a measurement, not a guess about names or folders: the file is opened
		/// exclusively, which succeeds only if no other process has it open. Inside that window the
		/// truncation cannot race anybody.
		///
		/// Truncate first, then delete: the truncation is certain to work once the exclusive handle is
		/// held, while the deletion can still be refused - by a read-only attribute, or by the folder's
		/// permissions - and a file that has been emptied is cleared either way. Deleting on top of
		/// that only keeps the folder tidy.
		/// </remarks>
		void Clear( string path )
		{
			try
			{
				using( var file = new FileStream( path, FileMode.Open, FileAccess.ReadWrite, FileShare.None ) )
				{
					file.SetLength( 0 );
				}
			}
			catch( IOException e )
			{
				// held open by somebody - the normal state of a log on a running system
				MarkOnly( path, "In use, marked instead of cleared" );
				log.Debug( $"Could not open '{path}' exclusively ({e.Message}); marked instead." );
				return;
			}

			var deleted = true;
			try
			{
				File.Delete( path );
			}
			catch( Exception e )
			{
				deleted = false;
				log.Debug( $"Emptied '{path}' but could not delete it: {e.Message}" );
			}

			// nothing left to collect from before the line, so the mark would only get in the way
			_marks!.Unmark( path );

			_result.Cleared++;
			_result.Details.Add( deleted ? $"Deleted {path}" : $"Emptied {path}" );
		}

		void MarkOnly( string path, string what )
		{
			var mark = _marks!.MarkFile( path );
			if( mark is null )
			{
				_result.Failed++;
				_result.Details.Add( $"Could not mark {path}" );
				return;
			}

			_result.Marked++;
			_result.Details.Add( $"{what} at {mark.Offset} bytes: {path}" );
		}
	}
}
