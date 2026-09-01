using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Dirigent;

namespace Dirigent.Scripts.BuiltIn
{

	/*
	* Takes a bunch of vfsNodes. Produces a zip package from those located on this machine.
	* Uploads the zip file to given destination.
	*
	* The files are streamed into the archive one by one, the virtual folder structure becoming the
	* entry names. Nothing is copied anywhere first: the archive layout exists in no file system, so
	* the earlier approach of materializing it in a temp folder for ZipFile.CreateFromDirectory meant
	* writing and reading every file once more - unaffordable for the multi-gigabyte log files that
	* an unrotated logger produces.
	*/
	public class DownloadZippedSlave : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/DownloadZippedSlave.cs";

		//[MessagePack.MessagePackObject]
		public class TArgs
		{
			// vfsnode contained (package, folder, virtual folder..); only the child nodes matter
			//[MessagePack.Key( 1 )]
			public VfsNodeDef? Container;

			// Where to upload the zip file, as seen from a machine that does not own the folder,
			// i.e. a UNC path. Empty if no file share covers the folder - then only a machine
			// owning the folder can upload.
			//[MessagePack.Key( 2 )]
			public string? DestinationFolder;

			// The very same folder as a local path on the machine that owns it. Used in preference
			// to the UNC path by a slave running on that machine - copying to our own disk through
			// a share is pointless work, and without a share it is not even possible.
			//[MessagePack.Key( 5 )]
			public string? LocalDestinationFolder;

			// Whether this slave runs on the machine owning the destination folder, i.e. whether
			// LocalDestinationFolder is a path this slave can use.
			//[MessagePack.Key( 6 )]
			public bool DestinationIsLocal;

			// Name of the zip file to create in the destination folder, excluding extension
			//[MessagePack.Key( 3 )]
			public string? ZipFileBaseName;

			// zip also files that are not associated with any machine (one of machine needs to do it)
			//[MessagePack.Key( 4 )]
			public bool IncludeGlobals;

			public override string ToString() => $"{Container} => {DestinationFolder}/{ZipFileBaseName}";
		};

		//[MessagePack.MessagePackObject]
		public class TResult
		{
			//[MessagePack.Key( 1 )]
			public string ZipFileName = "";

			//[MessagePack.Key( 2 )]
			public List<SerializedException> Exceptions = new();

			/// <summary>How many files were cut at a mark rather than collected whole.</summary>
			//[MessagePack.Key( 3 )]
			public int MarkedFileCount;

			/// <summary>
			/// The oldest mark any of them was cut at - the beginning of the window this archive covers.
			/// </summary>
			//[MessagePack.Key( 4 )]
			public DateTime? EarliestMark;

			/// <summary>
			/// What drew those lines - "Clear" or "Mark" - or empty if the files disagree, which
			/// happens when somebody marked, then cleared only part of the system.
			/// </summary>
			//[MessagePack.Key( 5 )]
			public string MarkedBy = string.Empty;
		}

		/// <summary>
		/// What this slave publishes while it works, so that the script that started it can weigh the
		/// machines against each other instead of averaging them as equals.
		/// </summary>
		public class TProgress
		{
			public long BytesDone;
			public long BytesTotal;
			public string? CurrentFile;
		}

		/// <summary>
		/// Write the archive in chunks this big. A destination reached over a file share is written
		/// to in as few round trips as the compressed data allows.
		/// </summary>
		const int _writeBufferBytes = 1024 * 1024;

		/// <summary>How much of a source file is read at a time - also how often a cancel is noticed.</summary>
		const int _copyBufferBytes = 256 * 1024;

		/// <summary>
		/// How much has to be collected before saying so again. One huge file must not sit at the
		/// same number for minutes, and a folder of small ones must not flood the network.
		/// </summary>
		const long _progressReportBytes = 4 * 1024 * 1024;

		TArgs? _args;

		long _bytesTotal;
		long _bytesDone;
		long _bytesAtLastReport;

		// what the marks did to this collection, for the note that goes into the archive
		int _markedFileCount;
		DateTime? _earliestMark;
		readonly HashSet<string> _markedBy = new( StringComparer.OrdinalIgnoreCase );

		protected override Task<string?> Run()
		{
			_args = Tools.Deserialize<TArgs>( Args );
			if( _args is null ) throw new NullReferenceException("Args == null");

			//throw new Exception( "Hey, test exception from a script! " + _Name );

			var destFileName = $"{_args.ZipFileBaseName}_{Dirig.Name}.zip";

			// what is ahead of us, so that the progress can be a fraction rather than a byte count
			_bytesTotal = TotalBytesToCollect( _args.Container! );
			ReportProgress( null, force: true );

			// exceptions gathered along the way (missing files etc.) - they do not stop the download
			var exceptions = WriteArchive( destFileName );

			// all done!
			var result = new TResult
			{
				ZipFileName = destFileName,
				Exceptions = SerializedException.MkList( exceptions ),
				MarkedFileCount = _markedFileCount,
				EarliestMark = _earliestMark,

				// one word only when every file agrees; a mixture is not worth naming
				MarkedBy = _markedBy.Count == 1 ? _markedBy.First() : string.Empty,
			};
			return Task.FromResult( Tools.Serialize(result) )!;
		}

		/// <summary>
		/// Builds the archive in the destination folder itself, under a temporary name, and moves it
		/// into place once it is complete. Tries the folders in order of preference; the local path
		/// first when this very machine owns it, since going through a file share to our own disk
		/// would be pointless work and in a network with no share defined is not even possible.
		/// </summary>
		/// <remarks>
		/// Writing straight to the destination saves writing the whole archive locally and reading it
		/// back to copy it - which for a large collection is most of the work left after the files
		/// themselves are no longer copied. The two-step name is what makes it safe: the merging step
		/// looks for the parts by name, and must never find one that is still being written.
		/// </remarks>
		List<Exception> WriteArchive( string destFileName )
		{
			Exception? firstFailure = null;

			foreach( var folder in DestinationFolders() )
			{
				// per attempt: a second destination must not inherit the complaints of the first
				var exceptions = new List<Exception>();
				var notes = new List<string>();
				var partPath = string.Empty;

				try
				{
					Directory.CreateDirectory( folder );

					var finalPath = Path.Combine( folder, destFileName );
					partPath = finalPath + ".part";

					// the destination file is opened before anything is compressed, so an unreachable
					// share costs nothing but the attempt
					using( var file = new FileStream( partPath, FileMode.Create, FileAccess.Write,
													FileShare.None, _writeBufferBytes ) )
					using( var zip = new ZipArchive( file, ZipArchiveMode.Create ) )
					{
						// same-named files coming from different places must not overwrite each other,
						// and the archive is the only place the names live now
						var usedEntryNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

						AddLocalFiles( _args!.Container!, string.Empty, zip, usedEntryNames, exceptions, notes );

						AddNotes( zip, usedEntryNames, notes );
					}

					File.Move( partPath, finalPath, true );

					return exceptions;
				}
				catch( OperationCanceledException )
				{
					// the user asked us to stop: take the half-written archive with us and do not
					// try the next destination, which would start the whole thing again
					DeletePart( partPath );
					throw;
				}
				catch( Exception e )
				{
					log.Warn( $"Could not write {destFileName} to '{folder}': {e.Message}" );
					if( firstFailure is null ) firstFailure = e;

					DeletePart( partPath );
				}
			}

			throw firstFailure ?? new Exception(
				$"No destination folder to write {destFileName} to. The machine holding the download "
				+ $"folder is not this one and no file share of it covers the folder." );
		}

		/// <summary>
		/// Removes a half-written archive. It never had the final name, so nothing has seen it.
		/// </summary>
		static void DeletePart( string partPath )
		{
			if( string.IsNullOrEmpty( partPath ) ) return;

			try { File.Delete( partPath ); }
			catch( Exception e ) { log.Warn( $"Could not delete {partPath}: {e.Message}" ); }
		}

		/// <summary>
		/// How many bytes this machine is going to put into the archive - the sizes as they will be
		/// collected, so a file taken by its tail counts as the tail only.
		/// </summary>
		long TotalBytesToCollect( VfsNodeDef container )
		{
			long total = 0;

			foreach( var node in container.Children )
			{
				if( node.IsContainer )
				{
					total += TotalBytesToCollect( node );
					continue;
				}

				if( !IsLocalNode( node ) && !( IsGlobalNode( node ) && _args!.IncludeGlobals ) )
					continue;

				// a file we cannot even measure is one that will fail to be collected as well;
				// the failure is reported from there, this is only an estimate
				try { total += EffectiveSize( node ); }
				catch {}
			}

			return total;
		}

		/// <summary>
		/// Publishes how far this machine has got, unless it said so a moment ago already.
		/// </summary>
		void ReportProgress( string? currentFile, bool force = false )
		{
			if( !force && _bytesDone - _bytesAtLastReport < _progressReportBytes )
				return;

			_bytesAtLastReport = _bytesDone;

			// an empty collection is done as soon as it starts; without a total there is no fraction
			double? progress = _bytesTotal > 0
								? Math.Min( 1.0, (double) _bytesDone / _bytesTotal )
								: ( _bytesDone > 0 ? null : (double?) 1.0 );

			var text = currentFile is null
						? $"{FileTail.FormatSize( _bytesTotal )} to collect"
						: $"{currentFile} ({FileTail.FormatSize( _bytesDone )} of {FileTail.FormatSize( _bytesTotal )})";

			SetStatus(
				text,
				Serialize( new TProgress()
				{
					BytesDone = _bytesDone,
					BytesTotal = _bytesTotal,
					CurrentFile = currentFile,
				} ),
				progress
			);
		}

		/// <summary>
		/// The folders to try uploading to, in order of preference.
		/// </summary>
		IEnumerable<string> DestinationFolders()
		{
			// our own disk first, if the folder is on it
			if( _args!.DestinationIsLocal && !string.IsNullOrEmpty( _args.LocalDestinationFolder ) )
				yield return _args.LocalDestinationFolder;

			// otherwise (or if the local attempt failed) through the file share
			if( !string.IsNullOrEmpty( _args!.DestinationFolder ) )
				yield return _args.DestinationFolder;

			// deliberately no local fallback for a machine that does not own the folder: it would
			// silently write to a same-named folder on the wrong machine
		}

		bool IsLocalNode( VfsNodeDef node )
		{
			return node.MachineId == Dirig.Name;
		}

		bool IsGlobalNode( VfsNodeDef node )
		{
			return string.IsNullOrEmpty(node.MachineId);
		}
		

		/// <summary>
		/// Walks the resolved vfs tree and streams the files living on this machine into the archive,
		/// the virtual folder structure becoming the entry names.
		/// </summary>
		/// <param name="entryPrefix">Archive path of the container, empty for the root, "a/b/" otherwise.</param>
		void AddLocalFiles( VfsNodeDef container, string entryPrefix, ZipArchive zip,
				HashSet<string> usedEntryNames, List<Exception> exceptions, List<string> notes )
		{
			// anything the resolution left out, so that the archive itself says it is incomplete
			if( container.Notes is not null )
				notes.AddRange( container.Notes );

			foreach( var node in container.Children )
			{
				if( node.IsContainer )
				{
					try
					{
						AddLocalFiles( node, entryPrefix + GetFolderName( node ) + "/", zip, usedEntryNames, exceptions, notes );
					}
					catch( OperationCanceledException )
					{
						throw; // a cancel ends the whole collection, it is not a per-file problem
					}
					catch (Exception e)
					{
						exceptions.Add( e );
					}
				}
				else
				{
					if( IsLocalNode( node )
					         ||
					   (IsGlobalNode(node) && _args!.IncludeGlobals)
					)
					{
						try
						{
							AddFile( zip, AppFolderFor( node, entryPrefix ), node, usedEntryNames, notes );
						}
						catch( OperationCanceledException )
						{
							throw; // a cancel ends the whole collection, it is not a per-file problem
						}
						catch (Exception e)
						{
							exceptions.Add( e );
						}
					}
				}
			}
		}

		/// <summary>
		/// Writes what the collection left out or cut short into a text entry at the root of the archive.
		/// </summary>
		/// <remarks>
		/// Without it an archive missing half a log folder looks exactly like a complete one, and
		/// whoever opens it months later has no way to tell.
		/// </remarks>
		void AddNotes( ZipArchive zip, HashSet<string> usedEntryNames, List<string> notes )
		{
			if( notes.Count == 0 ) return;

			var text = new StringBuilder();
			text.AppendLine( $"What {Dirig.Name} could not put into this archive in full:" );
			text.AppendLine();
			foreach( var note in notes )
			{
				text.AppendLine( note );
				text.AppendLine();
			}

			var entry = zip.CreateEntry( MakeUniqueEntryName( "_incomplete.txt", usedEntryNames ), CompressionLevel.Fastest );
			using var stream = entry.Open();
			using var writer = new StreamWriter( stream, Encoding.UTF8 );
			writer.Write( text.ToString() );
		}

		/// <summary>
		/// Where the collection of a file begins - the start of it, the start of its tail, or the mark
		/// somebody left on it before a test run.
		/// </summary>
		/// <remarks>
		/// The two cuts compose by taking whichever is later, and each of them is a ceiling on how
		/// much can be delivered: a mark inside the tail leaves the tail's start standing, because the
		/// bytes before it are the ones that cannot be transferred at all; a tail shorter than the run
		/// cuts the run short for the same reason. The entry is named and headed after whichever cut
		/// won, so that the archive says which of the two limits was the binding one.
		/// </remarks>
		(long RawStart, bool ByMark, string? MarkNote) WhereToStart( VfsNodeDef node, FileInfo info )
		{
			var tailStart = FileTail.RawTailStart( info.Length, node.TailBytes );

			// only a file the configuration allows to be marked can carry a mark; a stale one is
			// dropped by the store itself, with a note saying so
			if( MarkStore is null || !node.Clearable )
				return ( tailStart, false, null );

			var (markStart, markNote) = MarkStore.WhereToStart( node.Path!, info.Length, info.CreationTimeUtc );

			return markStart >= tailStart && markStart > 0
					? ( markStart, true, markNote )
					: ( tailStart, false, markNote ); // the note still explains a stale mark
		}

		/// <summary>
		/// How much of a file will really be collected - what the progress should count.
		/// </summary>
		long EffectiveSize( VfsNodeDef node )
		{
			var info = new FileInfo( node.Path! );
			var (rawStart, _, _) = WhereToStart( node, info );
			return Math.Max( 0, info.Length - rawStart );
		}

		/// <summary>
		/// Streams one file into the archive, taking only its tail, or only what came after a mark,
		/// if either applies.
		/// </summary>
		void AddFile( ZipArchive zip, string entryPrefix, VfsNodeDef node,
				HashSet<string> usedEntryNames, List<string> notes )
		{
			var filePath = node.Path!;
			var info = new FileInfo( filePath );

			var (rawStart, byMark, markNote) = WhereToStart( node, info );
			var truncate = rawStart > 0;

			// a partial file is named for what it holds, so that the archive listing alone shows
			// which files are partial and what decided that
			var fileName = !truncate
							? Path.GetFileName( filePath )
							: ( byMark
								? FileTail.MarkedEntryNameFor( Path.GetFileName( filePath ) )
								: FileTail.EntryNameFor( Path.GetFileName( filePath ), node.TailBytes ) );

			var entryName = MakeUniqueEntryName( entryPrefix + fileName, usedEntryNames );

			// The share flags are not optional here:
			//  - ReadWrite, because a log file is typically held open for writing by the application
			//    producing it, and an open that does not permit that access is refused;
			//  - Delete, because we now hold the file open for the whole time it takes to compress it
			//    (minutes, for a huge log) rather than for a quick copy, and without it a logger
			//    rotating its file in that window would fail to rename it.
			// Opening before creating the entry keeps an unreadable file from leaving an empty
			// entry behind in the archive.
			using var src = new FileStream( filePath, FileMode.Open, FileAccess.Read,
											FileShare.ReadWrite | FileShare.Delete );

			var entry = zip.CreateEntry( entryName, CompressionLevel.Fastest );
			entry.LastWriteTime = ZipTimeOf( info.LastWriteTime );

			using var dst = entry.Open();

			if( truncate && byMark )
			{
				// remembered for the cover note: an archive of files cut at a mark covers a window,
				// and its beginning is worth stating once at the top rather than per entry
				_markedFileCount++;

				var mark = MarkStore?.Get( filePath );
				if( mark is not null )
				{
					if( _earliestMark is null || mark.MarkedAt < _earliestMark )
						_earliestMark = mark.MarkedAt;

					_markedBy.Add( FileMarkStore.OperationOf( mark ) );
				}
			}

			if( truncate )
			{
				// the length is read from the open stream, not from the FileInfo: a live log grows
				var startedAt = FileTail.SeekToStart( src, rawStart );
				var taken = src.Length - startedAt;

				// the entry has to say what it is, for whoever opens the archive with no access
				// to the configuration that made it
				var header = Encoding.UTF8.GetBytes( byMark
						? FileTail.PartialHeaderFor( filePath, src.Length, taken, DateTime.Now, markNote! )
						: FileTail.HeaderFor( filePath, src.Length, taken, DateTime.Now ) );
				dst.Write( header, 0, header.Length );

				notes.Add( byMark
						? $"'{filePath}' is {src.Length} bytes; only the {taken} written {markNote} were"
							+ $" collected, as '{entryName}'."
						: $"'{filePath}' is {src.Length} bytes; only its last {taken} were collected,"
							+ $" as '{entryName}' (TailBytes={node.TailBytes} on node '{node.Id}')." );
			}
			else if( markNote is not null )
			{
				// a mark that no longer fits the file: the whole file is collected instead of the run,
				// and the archive has to say why it holds more than was asked for
				notes.Add( $"'{filePath}' was collected in full - {markNote}." );
			}

			CopyWithProgress( src, dst, Path.GetFileName( filePath ) );
		}

		/// <summary>
		/// Copies the rest of a source stream into the archive, telling the world how it goes and
		/// stopping where the user asked for it.
		/// </summary>
		/// <remarks>
		/// Chunked rather than Stream.CopyTo for the sake of those two: a single copy call of a
		/// 60 GB log would report nothing and ignore a cancel for the several minutes it takes.
		/// </remarks>
		void CopyWithProgress( Stream src, Stream dst, string fileName )
		{
			var buffer = new byte[_copyBufferBytes];

			int read;
			while( ( read = src.Read( buffer, 0, buffer.Length ) ) > 0 )
			{
				CancellationToken.ThrowIfCancellationRequested();

				dst.Write( buffer, 0, read );

				_bytesDone += read;
				ReportProgress( fileName );
			}
		}

		/// <summary>
		/// The file's modification time, as the zip format is able to store it.
		/// </summary>
		/// <remarks>
		/// Zip keeps DOS timestamps, which start in 1980; ZipArchiveEntry.LastWriteTime throws for
		/// anything earlier. Setting it at all matters: an entry created by hand would otherwise
		/// carry the time the archive was made, losing the age of every collected file.
		/// </remarks>
		static DateTimeOffset ZipTimeOf( DateTime lastWriteTime )
		{
			var earliest = new DateTime( 1980, 1, 1, 0, 0, 0, DateTimeKind.Unspecified );
			return new DateTimeOffset( lastWriteTime < earliest ? earliest : lastWriteTime );
		}

		/// <summary>
		/// The archive folder to put a file into: the folder of its container, plus a subfolder named
		/// after the application the file belongs to.
		/// </summary>
		/// <remarks>
		/// The application subfolder is what keeps the same-named log files of several applications
		/// apart within one archive: in the usual layout every application's log node resolves to a
		/// container of the same title ("Recent logs"), so those collapse into one path and the
		/// application name is the only thing telling the files apart.
		///
		/// It is skipped when a folder of that name is already somewhere on the path, which happens
		/// wherever a container is named after the application - a node titled or id'd like the app
		/// (giving "log/cgfx/cgfx/app.log"), or an untitled &lt;Folder&gt; over the app's own directory
		/// (giving "cgfx/logs/cgfx/app.log"). The whole path is checked, not just the enclosing folder,
		/// because as the second example shows the repetition need not be adjacent.
		/// </remarks>
		static string AppFolderFor( VfsNodeDef node, string entryPrefix )
		{
			if( string.IsNullOrEmpty( node.AppId ) )
				return entryPrefix;

			var appFolder = SanitizeName( node.AppId );

			foreach( var folder in entryPrefix.Split( '/', StringSplitOptions.RemoveEmptyEntries ) )
			{
				if( folder.Equals( appFolder, StringComparison.OrdinalIgnoreCase ) )
					return entryPrefix; // said already, saying it again tells the reader nothing
			}

			return entryPrefix + appFolder + "/";
		}

		/// <summary>
		/// Name of the archive subfolder to put the content of given container node into.
		/// </summary>
		static string GetFolderName( VfsNodeDef node )
		{
			var name = node.Title;
			if( string.IsNullOrEmpty( name ) ) name = Path.GetFileName( node.Path ?? "" );
			if( string.IsNullOrEmpty( name ) ) name = node.Id;
			if( string.IsNullOrEmpty( name ) ) name = "folder";
			return SanitizeName( name );
		}

		/// <summary>
		/// Makes given string usable as a single file/folder name.
		/// Node titles may contain submenu separators and characters not allowed in a path.
		/// </summary>
		static string SanitizeName( string name )
		{
			// a title like "Logs/Recent" denotes a submenu path - take just the last segment of it
			var lastSegment = name.Split( new char[] {'/','\\'}, StringSplitOptions.RemoveEmptyEntries ).LastOrDefault() ?? name;

			var invalid = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder();
			foreach( var c in lastSegment )
			{
				sb.Append( Array.IndexOf( invalid, c ) >= 0 ? '_' : c );
			}

			var res = sb.ToString().Trim( ' ', '.' );
			return string.IsNullOrEmpty( res ) ? "_" : res;
		}

		/// <summary>
		/// Adds a numbered suffix if the entry name is taken already, so that same-named files
		/// coming from different places do not overwrite each other within the archive.
		/// </summary>
		static string MakeUniqueEntryName( string entryName, HashSet<string> usedEntryNames )
		{
			if( usedEntryNames.Add( entryName ) )
				return entryName;

			var lastSlash = entryName.LastIndexOf( '/' );
			var folder = lastSlash < 0 ? string.Empty : entryName.Substring( 0, lastSlash + 1 );
			var fileName = entryName.Substring( lastSlash + 1 );
			var name = Path.GetFileNameWithoutExtension( fileName );
			var ext = Path.GetExtension( fileName );

			for( int i = 2; i < 1000; i++ )
			{
				var candidate = $"{folder}{name}_{i}{ext}";
				if( usedEntryNames.Add( candidate ) )
					return candidate;
			}

			return entryName; // give up, let the duplicate happen
		}
	}

}
