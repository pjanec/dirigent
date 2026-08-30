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
	* Merges the per-machine zip archives uploaded to a staging folder into one single archive.
	* Runs on the machine owning the staging folder, so that it works with local files only.
	* The staging folder is removed afterwards.
	*/
	public class MergeZipped : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/MergeZipped.cs";

		//[MessagePack.MessagePackObject]
		public class TPart
		{
			// name of the zip file within the staging folder
			//[MessagePack.Key( 1 )]
			public string FileName = "";

			// machine the archive came from
			//[MessagePack.Key( 2 )]
			public string MachineName = "";

			public override string ToString() => $"{FileName} ({MachineName})";
		}

		//[MessagePack.MessagePackObject]
		public class TArgs
		{
			// folder holding the archives to merge; local to the machine running this script
			//[MessagePack.Key( 1 )]
			public string? StagingFolder;

			// full path of the archive to produce; local to the machine running this script
			//[MessagePack.Key( 2 )]
			public string? DestinationFile;

			// what archives to merge, in the order they should appear in the result
			//[MessagePack.Key( 3 )]
			public List<TPart> Parts = new();

			// put the content of each archive into a folder named after the machine it came from
			//[MessagePack.Key( 4 )]
			public bool PrefixWithMachine = true;

			public override string ToString() => $"{Parts.Count} parts => {DestinationFile}";
		};

		//[MessagePack.MessagePackObject]
		public class TResult
		{
			// name of the resulting archive, empty if there was nothing to merge
			//[MessagePack.Key( 1 )]
			public string ZipFileName = "";

			//[MessagePack.Key( 2 )]
			public int FileCount;

			//[MessagePack.Key( 3 )]
			public List<SerializedException> Exceptions = new();
		}

		protected override Task<string?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args );
			if( args is null ) throw new NullReferenceException("Args == null");

			return Task.FromResult( Tools.Serialize( Merge( args, CancellationToken ) ) )!;
		}

		/// <summary>
		/// Joins the archives listed in the arguments into a single one and removes the staging folder.
		/// </summary>
		public static TResult Merge( TArgs args, CancellationToken ct = default )
		{
			if( string.IsNullOrEmpty( args.StagingFolder ) ) throw new NullReferenceException("Args.StagingFolder is empty");
			if( string.IsNullOrEmpty( args.DestinationFile ) ) throw new NullReferenceException("Args.DestinationFile is empty");

			var exceptions = new List<Exception>(); // gathered from the merging of the individual archives
			var result = new TResult();

			try
			{
				// nothing arrived - do not produce an empty archive
				if( args.Parts.Count == 0 )
					return result;

				// a single archive with no machine folder wanted is just a rename
				if( args.Parts.Count == 1 && !args.PrefixWithMachine )
				{
					var singlePart = Path.Combine( args.StagingFolder, args.Parts[0].FileName );
					File.Move( singlePart, args.DestinationFile, true );

					result.ZipFileName = Path.GetFileName( args.DestinationFile );
					result.FileCount = CountEntries( args.DestinationFile );
					return result;
				}

				var usedEntryNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

				using( var dstZip = ZipFile.Open( args.DestinationFile, ZipArchiveMode.Create ) )
				{
					foreach( var part in args.Parts )
					{
						var partPath = Path.Combine( args.StagingFolder, part.FileName );

						try
						{
							using var srcZip = ZipFile.OpenRead( partPath );

							foreach( var srcEntry in srcZip.Entries )
							{
								// skip the folder entries, they get created by the files anyway
								if( srcEntry.FullName.EndsWith( "/" ) || srcEntry.FullName.EndsWith( "\\" ) )
									continue;

								var entryName = args.PrefixWithMachine
													? $"{SanitizeName( part.MachineName )}/{srcEntry.FullName}"
													: srcEntry.FullName;

								entryName = MakeUniqueEntryName( entryName, usedEntryNames );

								var dstEntry = dstZip.CreateEntry( entryName, CompressionLevel.Fastest );

								ct.ThrowIfCancellationRequested();

								using var srcStream = srcEntry.Open();
								using var dstStream = dstEntry.Open();
								srcStream.CopyTo( dstStream );

								result.FileCount++;
							}
						}
						catch( OperationCanceledException )
						{
							throw; // a cancel ends the merge, it is not a problem with this one part
						}
						catch( Exception e )
						{
							// a damaged or missing part must not cost us the rest of the download
							exceptions.Add( new Exception( $"{part.MachineName}: {e.Message}" ) );
						}
					}
				}

				result.ZipFileName = Path.GetFileName( args.DestinationFile );

				return result;
			}
			catch( OperationCanceledException )
			{
				// unlike the parts, the merged archive is written under its final name, so a
				// half-written one would look like the download had succeeded
				try
				{
					if( File.Exists( args.DestinationFile ) )
						File.Delete( args.DestinationFile );
				}
				catch( Exception e )
				{
					log.Debug( $"Could not remove the unfinished archive {args.DestinationFile}: {e.Message}" );
				}

				throw;
			}
			finally
			{
				result.Exceptions = SerializedException.MkList( exceptions );

				// the staging folder has no use anymore, whatever the outcome was
				try
				{
					if( Directory.Exists( args.StagingFolder ) )
						Directory.Delete( args.StagingFolder, true );
				}
				catch( Exception e )
				{
					log.Debug( $"Could not remove the staging folder {args.StagingFolder}: {e.Message}" );
				}
			}
		}

		static int CountEntries( string zipFilePath )
		{
			try
			{
				using var zip = ZipFile.OpenRead( zipFilePath );
				return zip.Entries.Count( x => !x.FullName.EndsWith( "/" ) && !x.FullName.EndsWith( "\\" ) );
			}
			catch( Exception )
			{
				return 0;
			}
		}

		/// <summary>
		/// Makes given string usable as a single folder name within the archive.
		/// </summary>
		static string SanitizeName( string name )
		{
			var invalid = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder();
			foreach( var c in name )
			{
				sb.Append( Array.IndexOf( invalid, c ) >= 0 ? '_' : c );
			}

			var res = sb.ToString().Trim( ' ', '.' );
			return string.IsNullOrEmpty( res ) ? "_" : res;
		}

		/// <summary>
		/// Guards against two archives carrying the same entry name, which would produce
		/// an archive with duplicate entries.
		/// </summary>
		static string MakeUniqueEntryName( string entryName, HashSet<string> usedEntryNames )
		{
			if( usedEntryNames.Add( entryName ) )
				return entryName;

			var folder = Path.GetDirectoryName( entryName )?.Replace( '\\', '/' ) ?? string.Empty;
			var name = Path.GetFileNameWithoutExtension( entryName );
			var ext = Path.GetExtension( entryName );

			for( int i = 2; i < 1000; i++ )
			{
				var candidate = string.IsNullOrEmpty( folder ) ? $"{name}_{i}{ext}" : $"{folder}/{name}_{i}{ext}";
				if( usedEntryNames.Add( candidate ) )
					return candidate;
			}

			return entryName; // give up, let the duplicate happen
		}
	}

}
