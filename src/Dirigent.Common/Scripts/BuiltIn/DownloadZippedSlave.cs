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
		}

		TArgs? _args;
		
		protected override Task<string?> Run()
		{
			_args = Tools.Deserialize<TArgs>( Args );
			if( _args is null ) throw new NullReferenceException("Args == null");

			//throw new Exception( "Hey, test exception from a script! " + _Name );

			var exceptions = new List<Exception>(); // exceptions gathered from the execution of this script (missing files etc.)

			// the archive is built locally and uploaded afterwards; only the compressed result
			// travels, which is the point of zipping on the machine owning the files
			var zipFileFullPath = Path.Combine( Path.GetTempPath(), Path.GetRandomFileName() + ".zip" );

			try
			{
				using( var zip = ZipFile.Open( zipFileFullPath, ZipArchiveMode.Create ) )
				{
					// same-named files coming from different places must not overwrite each other,
					// and the archive is the only place the names live now
					var usedEntryNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
					var notes = new List<string>();

					AddLocalFiles( _args.Container!, string.Empty, zip, usedEntryNames, exceptions, notes );

					AddNotes( zip, usedEntryNames, notes );
				}

				var destFileName = $"{_args.ZipFileBaseName}_{Dirig.Name}.zip";

				// upload the zip file to wherever we can reach
				// (the destination may be a staging folder created by whoever gets there first)
				Upload( zipFileFullPath, destFileName );

				// all done!
				var result = new TResult { ZipFileName = destFileName, Exceptions = SerializedException.MkList( exceptions ) };
				return Task.FromResult( Tools.Serialize(result) )!;
			}
			finally
			{
				try { File.Delete( zipFileFullPath ); } catch( Exception e ) { log.Warn( $"Could not delete {zipFileFullPath}: {e.Message}" ); }
			}
		}

		/// <summary>
		/// Copies the archive to the destination folder, preferring the local path when this very
		/// machine owns the folder. Copying to our own disk through a file share would be pointless
		/// work, and in a network with no share defined it is not even possible.
		/// </summary>
		string Upload( string zipFileFullPath, string destFileName )
		{
			Exception? firstFailure = null;

			foreach( var folder in DestinationFolders() )
			{
				try
				{
					Directory.CreateDirectory( folder );

					var destFileFullPath = Path.Combine( folder, destFileName );
					File.Copy( zipFileFullPath, destFileFullPath, true );

					return destFileFullPath;
				}
				catch( Exception e )
				{
					log.Warn( $"Could not upload {destFileName} to '{folder}': {e.Message}" );
					if( firstFailure is null ) firstFailure = e;
				}
			}

			throw firstFailure ?? new Exception(
				$"No destination folder to upload {destFileName} to. The machine holding the download "
				+ $"folder is not this one and no file share of it covers the folder." );
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
							// files belonging to an app go to a subfolder named after the app, so that
							// the same-named log files of multiple apps do not clash within the archive
							var fileEntryPrefix = string.IsNullOrEmpty( node.AppId )
													? entryPrefix
													: entryPrefix + SanitizeName( node.AppId ) + "/";

							AddFile( zip, fileEntryPrefix, node, usedEntryNames, notes );
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
		/// Streams one file into the archive, taking only its tail if the node asks for that.
		/// </summary>
		static void AddFile( ZipArchive zip, string entryPrefix, VfsNodeDef node,
				HashSet<string> usedEntryNames, List<string> notes )
		{
			var filePath = node.Path!;
			var info = new FileInfo( filePath );
			var truncate = FileTail.Applies( info.Length, node.TailBytes );

			// a truncated file is named for what it holds, so that the archive listing alone shows
			// which files are partial
			var fileName = truncate
							? FileTail.EntryNameFor( Path.GetFileName( filePath ), node.TailBytes )
							: Path.GetFileName( filePath );

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

			if( truncate )
			{
				// the length is read from the open stream, not from the FileInfo: a live log grows
				var startedAt = FileTail.SeekToTailStart( src, node.TailBytes );
				var taken = src.Length - startedAt;

				// the entry has to say what it is, for whoever opens the archive with no access
				// to the configuration that made it
				var header = Encoding.UTF8.GetBytes(
					FileTail.HeaderFor( filePath, src.Length, taken, DateTime.Now ) );
				dst.Write( header, 0, header.Length );

				notes.Add( $"'{filePath}' is {src.Length} bytes; only its last {taken} were collected,"
						+ $" as '{entryName}' (TailBytes={node.TailBytes} on node '{node.Id}')." );
			}

			src.CopyTo( dst );
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
