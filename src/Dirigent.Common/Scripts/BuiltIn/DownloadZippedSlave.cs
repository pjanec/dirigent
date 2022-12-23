using System;
using System.Collections.Generic;
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
	* Before zipping the relevant files are copied to a temporary directory where the folder structure is created
	* according to the structure of the vfsnodes.
	*/
	public class DownloadZippedSlave : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/DownloadZippedSlave.cs";

		[MessagePack.MessagePackObject]
		public class TArgs
		{
			// vfsnode contained (package, folder, virtual folder..); only the child nodes matter
			[MessagePack.Key( 1 )]
			public VfsNodeDef? Container;

			// Where to upload the zip file. Should be UNC path if on remote machine.
			[MessagePack.Key( 2 )]
			public string? DestinationFolder;

			// Name of the zip file to create in the destination folder, excluding extension
			[MessagePack.Key( 3 )]
			public string? ZipFileBaseName;

			// zip also files that are not associated with any machine (one of machine needs to do it)
			[MessagePack.Key( 4 )]
			public bool IncludeGlobals;

			public override string ToString() => $"{Container} => {DestinationFolder}/{ZipFileBaseName}";
		};

		[MessagePack.MessagePackObject]
		public class TResult
		{
			[MessagePack.Key( 1 )]
			public string ZipFileName = "";

			[MessagePack.Key( 2 )]
			public List<SerializedException> Exceptions = new();
		}

		TArgs? _args;
		
		protected async override Task<byte[]?> Run( CancellationToken ct )
		{
			_args = Tools.Deserialize<TArgs>( Args );
			if( _args is null ) throw new NullReferenceException("Args == null");

			//throw new Exception( "Hey, test exception from a script! " + _Name );

			var exceptions = new List<Exception>(); // exceptions gathered from the execution of this script (missing files etc.)

			// create a unique temporary folder
			var tempFolder = Path.Combine( Path.GetTempPath(), Path.GetRandomFileName() );
			Directory.CreateDirectory( tempFolder );

			try
			{
				// traverse the vfs tree, create folders and copy local files to the temp folder
				CopyLocalFiles( _args.Container!, tempFolder, exceptions );

				// zip the content of the temp folder
				var zipFileFullPath = Path.Combine( Path.GetTempFileName()+".zip" );
				ZipFile.CreateFromDirectory( tempFolder, zipFileFullPath, CompressionLevel.Fastest, false  );

				try
				{
					// upload the zip file to the destination
					var destFileName = $"{_args.ZipFileBaseName}_{Dirig.Name}.zip";
					var destFileFullPath = Path.Combine( _args.DestinationFolder!, destFileName );
					File.Copy( zipFileFullPath, destFileFullPath, true );

					// all done!
					var result = new TResult { ZipFileName = destFileName, Exceptions = SerializedException.MkList( exceptions ) };
					return Tools.Serialize(result);
				}
				finally
				{
					File.Delete( zipFileFullPath );
				}
			}
			finally
			{
				// delete temp stuff
				Directory.Delete( tempFolder, true );
			}
		}

		bool IsLocalNode( VfsNodeDef node )
		{
			return node.MachineId == Dirig.Name;
		}

		bool IsGlobalNode( VfsNodeDef node )
		{
			return string.IsNullOrEmpty(node.MachineId);
		}
		

		void CopyLocalFiles( VfsNodeDef container, string destFolder, List<Exception> exceptions )
		{
			foreach( var node in container.Children )
			{
				if( node.IsContainer )
				{
					var newDestFolder = Path.Combine( destFolder, node.Title );
					try
					{
						Directory.CreateDirectory( newDestFolder );
						CopyLocalFiles( node, newDestFolder, exceptions );
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
						var destFile = Path.Combine( destFolder, Path.GetFileName(node.Path!) );
						try
						{
							File.Copy( node.Path!, destFile );
						}
						catch (Exception e)
						{
							exceptions.Add( e );
						}
					}
				}
			}
		}
	}

}
