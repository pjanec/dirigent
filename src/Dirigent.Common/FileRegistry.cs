
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;
using System.IO;
using System.Xml.Linq;
using System.Threading;
using System.IO.Enumeration;
using X = Dirigent.XmlConfigReaderUtils;

namespace Dirigent
{

	/// <summary>
	/// List of registered files and packages
	/// </summary>
	public class FileRegistry
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public List<FilePackageDef> PackageDefs { get; private set; } = new List<FilePackageDef>();

		// all VfsNodes found when traversing SharedDefs
		public Dictionary<Guid, VfsNodeDef> VfsNodes { get; private set; } = new Dictionary<Guid, VfsNodeDef>();
		
		PathPerspectivizer _pathPerspectivizer;
		MachineRegistry _machineRegistry;
		IDirig _ctrl;

		public FileRegistry( IDirig ctrl, MachineRegistry machineRegistry, PathPerspectivizer pathPerspectivizer )
		{
			_ctrl = ctrl;
			_machineRegistry = machineRegistry;
			_pathPerspectivizer = pathPerspectivizer;
		}
		
		public VfsNodeDef? GetVfsNodeDef( Guid guid )
		{
			if( VfsNodes.TryGetValue( guid, out var def ) ) return def;
			return null;
		}

		public IEnumerable<VfsNodeDef> GetAllVfsNodeDefs() => VfsNodes.Values;


		public void SetVfsNodes( IEnumerable<VfsNodeDef> vfsNodes )
		{
			VfsNodes = vfsNodes.ToDictionary( n => n.Guid );
		}


		public void Clear()
		{
			VfsNodes.Clear();
		}

		//public string GetMachineIP( string machineId )
		//{
		//	return _machineRegistry.GetMachineIP( machineId, "" );
		//}

		bool IsMatch( string? pattern, string? str )
		{
			if( string.IsNullOrEmpty( pattern ) ) // empty pattern means that anything matches
				return true;

			if( str is null ) // null string only matches if the pattern allows anything
				return pattern == "*";

			return FileSystemName.MatchesSimpleExpression( pattern, str );

			// wildcard pattern allowing single asterisk at the end
			//if (pattern.EndsWith("*") )
			//{
			//	string beforeAsterisk = pattern.Substring(0, pattern.Length-1);
			//	return str.StartsWith( beforeAsterisk, StringComparison.OrdinalIgnoreCase );
			//}

			//return string.Equals(str, pattern, StringComparison.OrdinalIgnoreCase);
		}

		List<VfsNodeDef> FindById( string Id, string? machineId, string? appId )
		{
			var res = new List<VfsNodeDef>();
			foreach( var node in VfsNodes.Values )
			{
				// empty string equals to null; this allows nullifying the machine/app inherited from parent node in shared config by using empty string
				if( !IsMatch( Id, node.Id ) )
					continue;
					
				if( !IsMatch( machineId, node.MachineId ) )
					continue;

				if( !IsMatch( appId, node.AppId ) )
					continue;

				// match!
				res.Add( node );
			}
			return res;
		}

		static T EmptyFrom<T>( VfsNodeDef x ) where T: VfsNodeDef, new()
		{
			var r = new T();
			//r.Guid = x.Guid; // gud should stay unique
			r.Id = x.Id;
			r.Title = x.Title;
			r.MachineId = x.MachineId;
			return r;
		}

		bool IsLocalMachine( string clientId )
		{
			return _machineRegistry.IsLocal( clientId );
		}

		Dictionary<string,string> GetExpansionVariables( VfsNodeDef nodeDef )
		{
			var vars = new Dictionary<string, string>();

			// expand variables in local context
			if( _machineRegistry.IsLocal( nodeDef.MachineId ) )
			{

				var localMachineIP = _machineRegistry.GetMachineIP( _machineRegistry.LocalMachineId );

				// for app-bound files, expand also local vars and define var for app working dir etc.
				{
					// KEEP IN SYNC WITH Launcher.cs
					vars["MACHINE_ID"] = _machineRegistry.LocalMachineId;
					vars["MACHINE_IP"] = localMachineIP;
					vars["DIRIGENT_MACHINE_ID"] = _machineRegistry.LocalMachineId;
					vars["DIRIGENT_MACHINE_IP"] = localMachineIP;
				
					if( !string.IsNullOrEmpty( nodeDef.AppId ) )
					{
						var appDef = _ctrl.GetAppDef( new AppIdTuple( nodeDef.MachineId!, nodeDef.AppId ) );
						if( appDef is not null )
						{
							foreach( var (k,v) in appDef.EnvVarsToSet )
								vars[k] = v;

							// add some app-special vars
							vars["DIRIGENT_APPID"] = appDef.Id.AppId;
							vars["APP_ID"] = appDef.Id.AppId;
							vars["APP_BINDIR"] = Tools.ExpandEnvAndInternalVars( Path.GetDirectoryName(appDef.ExeFullPath)!, appDef.EnvVarsToSet );
							vars["APP_STARTUPDIR"] = Tools.ExpandEnvAndInternalVars( appDef.StartupDir, appDef.EnvVarsToSet );
						}
					}
				}
			}
			
			return vars;
		}

		string? GetExpandedPath( VfsNodeDef vfsNode )
		{
			if (vfsNode.Path is null)
				return null;
			var vars = GetExpansionVariables( vfsNode );
			return Tools.ExpandEnvAndInternalVars( vfsNode.Path, vars );
		}
		

		/// <summary>
		///   Converts given VfsNode into a tree of virtual folders containing links to physical files.
		///   Resolves all links, scans the folders (remembering the contained files and subfolders if requested), expands variables.
		///   Variables used for expansion are the env vars of the dirigent process running on the file's machine.
		///   File paths returned stays local to the file's machine.
		/// </summary>
		/// <param name="def">Root node of what to resolve</param>
		/// <param name="includeContent">If true, will include content of folders, otherwise will just include the folders themselves</param>
		/// <returns>
		///   Folders - VFolder will have just the Title (vfolder name). Path only if the folder represents physical folder (not a virtual one).
		///   Files will have the Title and Path (link to physical file).
		/// </returns>
		public async Task<ExpandedVfsNodeDef?> ExpandPathsAsync( IDirigAsync iDirig, VfsNodeDef nodeDef, bool includeContent, List<Guid>? usedGuids )
		{
			if (nodeDef is null)
				throw new ArgumentNullException( nameof( nodeDef ) );
				
			if (usedGuids == null) usedGuids = new List<Guid>();
			if (usedGuids.Contains( nodeDef.Guid ))
			{
				//	throw new Exception( $"Circular reference in VFS tree: {nodeDef}" );
				return null;
			}
			
			usedGuids.Add( nodeDef.Guid );

			// non-local stuff to be always resolved on machine where local - via remote script call
			if( !string.IsNullOrEmpty(nodeDef.MachineId) // global resources are machine independent - can be resolved on any machine
				&& !IsLocalMachine(nodeDef.MachineId) )
			{
				// check if required machine is available
				if( !_machineRegistry.IsOnline( nodeDef.MachineId ) )
					throw new Exception($"Machine {nodeDef.MachineId} not connected.");
					
				// await script	to expand remotely
				var args = new Scripts.BuiltIn.ExpandVfsPath.TArgs
				{
					VfsNode = nodeDef,
					IncludeContent = includeContent
				};

				var result = await iDirig.RunScriptAsync<Scripts.BuiltIn.ExpandVfsPath.TArgs, Scripts.BuiltIn.ExpandVfsPath.TResult>(
						nodeDef.MachineId ?? "",
						Scripts.BuiltIn.ExpandVfsPath._Name,
						"",	// sourceCode
						args,
						$"ExpandPaths {nodeDef.Xml}",
						out var instance
					);

				return result!.VfsNode!;

			}

			// from here on, we are on local machine (or master)

			

			if( nodeDef is FileDef fileDef )
			{
				return ExpandFileDef( fileDef );
			}
			else
			if( nodeDef is FileRef fref )
			{
				return await ExpandFileRef( iDirig, includeContent, usedGuids, fref );
			}
			else
			if (nodeDef is VFolderDef vfolderDef)
			{
				return await ExpandVFolder( iDirig, vfolderDef, usedGuids );
			}
			else
			if( nodeDef is FolderDef folderDef )
			{
				return ExpandFolder( folderDef, includeContent );
			}
			else
			if( nodeDef is FilePackageDef fpdef )
			{
				return await ExpandVFolder( iDirig, fpdef, usedGuids );
			}
			else
			{
				throw new Exception( $"Unknown VfsNodeDef type: {nodeDef}" );
			}
		}

		private async Task<ExpandedVfsNodeDef?> ExpandFileRef( IDirigAsync iDirig, bool includeContent, List<Guid>? usedGuids, FileRef fref )
		{
			var defs = FindById( fref.Id, fref.MachineId, fref.AppId );

			// remove reference to self
			defs.RemoveAll( x => x.Guid == fref.Guid );

			if( defs.Count == 0 )
				return null;

			if ( defs.Count == 1 )
				return await ExpandPathsAsync( iDirig, defs[0], includeContent, usedGuids );

			{
				var pack = new ExpandedVfsNodeDef() { IsContainer = true };
				pack.Title = fref.Title;
				if ( string.IsNullOrEmpty(pack.Title) ) pack.Title = fref.Id;
				if( string.IsNullOrEmpty(pack.Title) ) pack.Title = fref.Guid.ToString();
				foreach( var def in defs )
				{
					var resolved = await ExpandPathsAsync( iDirig, def, includeContent, usedGuids );
					if( resolved is not null )
						pack.Children.Add( resolved );
				}
				return pack;
			}
				
			
		}

		private ExpandedVfsNodeDef? ExpandFileDef( FileDef fileDef )
		{
			if (string.IsNullOrEmpty( fileDef.Path )) throw new Exception( $"FileDef.Path is empty. {fileDef.Xml}" );


			//if( fileDef.Path.Contains('%') )

			if (string.IsNullOrEmpty( fileDef.Filter ))
			{
				var r = EmptyFrom<ExpandedVfsNodeDef>( fileDef );
				r.IsContainer = false;
				r.Guid = fileDef.Guid;
				r.Path = GetExpandedPath( fileDef );
				if( r.Path is null ) return null;
				return r;
			}

			// newest file(s) from folder?
			//  - if just one file is requested, return one single FileDef or null
			//  - if multiple files allowed, return VFolder
			if (fileDef.Filter.Equals( "newest", StringComparison.OrdinalIgnoreCase ))
			{
				return ExpandFileDef_Newest( fileDef );
			}

			throw new Exception( $"Unsupported filter. {fileDef.Xml}" );
		}

		// get newest file(s) from given folder (fileDef.Path = name of the folder)
		private ExpandedVfsNodeDef? ExpandFileDef_Newest( FileDef fileDef )
		{
			var folder = GetExpandedPath( fileDef );
			if (folder is null)
				return null;

			if (string.IsNullOrEmpty( fileDef.Xml )) throw new Exception( $"FileDef.Xml is empty. {fileDef.Xml}" );
			var xml = XElement.Parse( fileDef.Xml );

			string mask = X.getStringAttr( xml, "Mask", "*.*" );
			int maxFiles = X.getIntAttr( xml, "MaxFiles", 1 ); // by default a single file only
			if (maxFiles < 1) maxFiles = 1;
			double maxSeconds = X.getDoubleAttr( xml, "MaxSeconds", double.MaxValue ); // by default whatever age

			var newestFiles = GetNewestFilesInFolder( folder, mask, maxFiles, maxSeconds );

			// if just one single file requested, return FileDef
			if (maxFiles <= 1)
			{
				if (newestFiles.Count == 0)
				{
					return null;
				}
				else
				{
					var r = EmptyFrom<ExpandedVfsNodeDef>( fileDef );
					r.Guid = fileDef.Guid;
					r.Path = newestFiles[0];
					return r;
				}
			}
			else
			// if more files possible, put them in container
			{
				var pack = EmptyFrom<ExpandedVfsNodeDef>( fileDef );
				pack.IsContainer = true;
				if (string.IsNullOrEmpty( pack.Title )) pack.Title = pack.Id;
				if (string.IsNullOrEmpty( pack.Title )) pack.Title = pack.Guid.ToString();
				foreach (var fpath in newestFiles)
				{
					var r = EmptyFrom<FileDef>( fileDef );
					r.Guid = fileDef.Guid;
					r.Path = fpath;
					pack.Children.Add( r );
				}
				return pack;
			}
		}

		async Task<ExpandedVfsNodeDef> ExpandVFolder( IDirigAsync iDirig, VfsNodeDef folderDef, List<Guid>? usedGuids )
		{
			var rootNode = EmptyFrom<ExpandedVfsNodeDef>( folderDef ); // this produces Iscontainer=false (ExpandedVfsNode does not say if it is a container or not)
			rootNode.IsContainer = true;

			// FIXME: group children by machineId, resolve whole group by single remote script call
			foreach ( var child in folderDef.Children )
			{
				var resolved = await ExpandPathsAsync( iDirig, child, true, usedGuids );
				if( resolved is not null )
				{
					rootNode.Children.Add( resolved );
				}
			}

			return rootNode;
		}

		ExpandedVfsNodeDef? ExpandFolder( FolderDef folderDef, bool includeContent )
		{
			var folderPath = GetExpandedPath( folderDef );
			
			var rootNode = EmptyFrom<ExpandedVfsNodeDef>( folderDef );
			rootNode.IsContainer = true;
			rootNode.Path = folderPath;
			
			if( includeContent )
			{
				// FIXME:
				// traverse all files & folders 
				// filter by glob-style mask
				// convert into vfs tree structure
				if( string.IsNullOrEmpty(folderPath) )
					return null;
				// ....

				var mask = folderDef.Mask;
				if( string.IsNullOrEmpty(mask) ) mask = "*.*"; //throw new Exception($"No file mask given in '{pathWithMask}'");

				try
				{
					var dirs = FindDirectories( folderPath );
					foreach (var dir in dirs)
					{
						var dirDef = new FolderDef
						{
							//Id = dir.Name,
							Path = dir.FullName,
							MachineId = folderDef.MachineId,
							AppId = folderDef.AppId,
							IsContainer = true,
							Title = dir.Name,
						};
						var vfsFolder = ExpandFolder( dirDef, includeContent );
						if( vfsFolder is not null )
						{
							rootNode.Children.Add( vfsFolder );
						}
					}
				}
				catch( Exception ex ) // folder not exists or not accessible?
				{
					log.Debug($"ExpandFolder failed: {folderDef} Error: {ex.Message}");
					return null;
				}

				try
				{
					var files = FindMatchingFileInfos( folderPath, mask, false );
					foreach (var file in files)
					{
						var fileDef = new FileDef
						{
							//Id = file.Name,
							Path = file.FullName,
							MachineId = folderDef.MachineId,
							AppId = folderDef.AppId,
							IsContainer = false,
							Title = file.Name,
						};
						rootNode.Children.Add( fileDef );
					}
				}
				catch( Exception ex ) // folder not exists or not accessible?
				{
					log.Debug($"ExpandFolder failed: {folderDef} Error: {ex.Message}");
					return null;
				}

			}
			
			return rootNode;
		}

		List<string> GetNewestFilesInFolder( string folderName, string mask, int maxFiles, double maxAgeSeconds )
		{
			var res = new List<string>();
			var files = FindMatchingFileInfos( folderName, mask, false );

			if( files.Length == 0 ) return res;
			
			Array.Sort( files, (x, y) => x.LastWriteTimeUtc.CompareTo( y.LastWriteTimeUtc ) );

			int numFiles = 0;
			foreach (var file in files)
			{
				if (maxAgeSeconds > 0)
				{
					var age = (DateTime.UtcNow - file.LastWriteTimeUtc).TotalSeconds;
					if (age > maxAgeSeconds) continue;
				}

				res.Add( file.FullName );
				numFiles++;
				if (numFiles >= maxFiles) break;
			}
			return res;
		}

		static FileInfo[] FindMatchingFileInfos( string folderName, string mask, bool recursive )
		{
			if( string.IsNullOrEmpty(mask) ) mask = "*.*"; //throw new Exception($"No file mask given in '{pathWithMask}'");
			if( string.IsNullOrEmpty( folderName ) ) folderName = Directory.GetCurrentDirectory();
			var dirInfo = new DirectoryInfo(folderName);
			var enumOpts = new EnumerationOptions()
			{
				MatchType = MatchType.Win32,
				RecurseSubdirectories = recursive,
				ReturnSpecialDirectories = false
			};
			FileInfo[] files = dirInfo.GetFiles( mask, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
			return files;
		}

		static DirectoryInfo[] FindDirectories( string folderName )
		{
			var dirInfo = new DirectoryInfo(folderName);
			DirectoryInfo[] dirs = dirInfo.GetDirectories();
			return dirs;
		}

		static string? GetNewest( FileInfo[] files )
		{
			if( files.Length == 0 ) return null;
			Array.Sort( files, (x, y) => x.LastWriteTimeUtc.CompareTo( y.LastWriteTimeUtc ) );
			return files[files.Length-1].FullName;
		}
	}
}

