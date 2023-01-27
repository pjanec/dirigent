using System;
using System.Linq;
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
	* Takes a bunch of vfsNodes. Starts a slave script on each machine where the files are local.
	* Let then upload machine-specific zip files to our folder we create in Downloads folder.
	* Wait for the slave scripts to finish and show a "download finished" bubble.
	*/
	public class BrowseInDblCmdVirtPanel : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/BrowseInDblCmdVirtPanel.cs";

		//[MessagePack.MessagePackObject]
		public class TArgs : ScriptActionArgs
		{
		};

		//[MessagePack.MessagePackObject]
		public class TResult
		{
		}

		static string TempFilePrefix = "1B90C3B7-51DA-4E58-9A70-5C1F47E9BD02";

		protected async override Task<byte[]?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args );
			if( args is null ) throw new NullReferenceException("Args is null");
			if( args.VfsNode is null ) throw new NullReferenceException("Args.VfsNode is null");

			// if a single file, create artificial container containing this single file
			var container = Tools.Containerize( args.VfsNode );

			// create our own temp folder
			var tempFolder = Path.Combine( Path.GetTempPath(), TempFilePrefix );
			Directory.CreateDirectory( tempFolder );

			// Delete old temporary files to keep the temp folder clean
			DeleteOldFiles( tempFolder );

			//
			// Convert the container to a VirtualPanel config file, save to a temporary file
			//
			var vpConfSB = new StringBuilder();
			AddToVpConf( vpConfSB, container, "" );

			var vpConfFilePath = Path.Combine( tempFolder, Guid.NewGuid()+".txt" );
			File.WriteAllText( vpConfFilePath, vpConfSB.ToString() );

			//
			// generate DoubleCommander's startup lua script in a temp folder
			//
			var scriptContent = $@"
DC.ExecuteCommand(""cm_OpenVirtualFileSystemList"")
DC.ExecuteCommand(""cm_QuickSearch"", ""search=on"", ""files=on"", ""directories=off"", ""text=VirtualPanel"")
DC.ExecuteCommand(""cm_QuickSearch"", ""search=off"")
DC.ExecuteCommand(""cm_Open"")
DC.ExecuteCommand(""cm_ExecuteCmdLine"", ""<load {vpConfFilePath.Replace("\\", "\\\\")}"")
			";
			
			var scriptFilePath = Path.Combine( tempFolder, Guid.NewGuid()+".lua" );
			File.WriteAllText( scriptFilePath, scriptContent );

			// run the DoubleManager tool (must be modified to support running lua scripts at start - see https://github.com/pjanec/doublecmd)
			await Dirig.SendAsync( new Net.RunActionMessage
			{
				HostClientId = Requestor,
				Def = new ToolActionDef() { Name = "DoubleCommander", Args = $"--startupscript={scriptFilePath}" },
			} );

			// all done!
			var result = new TResult {};
			return Tools.Serialize(result);
		}

		/*
		   Example of the config file:

			00000410    01D91AAE 7AD03F40   \doublecmd
			00000410	01D90049 E9F10640	\doublecmd\doc	
			00000420	01D8CB3D B2C30B00	\doublecmd\doc\COPYING.FPC.txt	D:\Temp\dcportable\doublecmd\doc\COPYING.FPC.txt

		   Line format (spaces not included):
			 <code> <tab> <uuid1> <space> <uuid2> <tab> <virtual path> <tab> [<physical path>] <crlf>
		  
		   Code
		     00000410 = virtual folder
		     00000420 = physical file
		   
		   Uuid1 Uuid2 =  some kind of UUID (unrecognized purpose);
		     seems to be same for all physical files in a virtual folder
		   
		   Virtual path = path in the virtual panel
		   Physical path = path in the real file system (present for physical files only)
		 */
		void AddToVpConf( StringBuilder sb, VfsNodeDef node, string parentPath )
		{
			foreach (var child in node.Children)
			{
				var uuid = "0000000000000000"; ////Guid.NewGuid().ToString();
				var uuid12 = $"{uuid[0..8]} {uuid[8..16]}";
				var virtPath = $"{parentPath}\\{Path.GetFileName(child.Path)}";
				if (child.IsContainer)
				{
					sb.AppendLine( $"00000410\t{uuid12}\t{virtPath}\t" ); // not the tab at the end!
					AddToVpConf( sb, child, virtPath );
				}
				else
				{
					sb.AppendLine( $"00000420\t{uuid12}\t{virtPath}\t{child.Path}" );
				}
			}
		}

		void DeleteOldFiles( string folder )
		{
			var tempFiles = Directory.GetFiles( folder, TempFilePrefix + "*" );
			foreach (var f in tempFiles)
			{
				var fi = new FileInfo( f );
				if (fi.CreationTime - DateTime.Now > TimeSpan.FromDays( 1 ))
				{
					try
					{
						File.Delete( f );
					}
					catch
					{
					}
				}
			}
		}

		

	}

}
