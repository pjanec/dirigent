
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

		public delegate string? GetMachineIPDelegate( string machineId );

		public GetMachineIPDelegate _machineIPDelegate;

		string _rootForRelativePaths;

		public class TMachine
		{
			public string Id = string.Empty;
			public string? IP = string.Empty;  // will be replaced with real IP once found
			public Dictionary<string, string> Shares = new Dictionary<string, string>();
		}

		public List<FilePackageDef> PackageDefs { get; private set; } = new List<FilePackageDef>();

		// all VfsNodes found when traversing SharedDefs
		public Dictionary<Guid, VfsNodeDef> VfsNodes { get; private set; } = new Dictionary<Guid, VfsNodeDef>();
		
		public Dictionary<string, TMachine> Machines { get; private set; } = new Dictionary<string, TMachine>();

		private string _localMachineId = string.Empty;

		IDirig _ctrl;
		
		public FileRegistry( IDirig ctrl, string localMachineId, string rootForRelativePaths, GetMachineIPDelegate machineIdDelegate )
		{
			_ctrl = ctrl;
			_localMachineId = localMachineId;
			_rootForRelativePaths = rootForRelativePaths;
			_machineIPDelegate = machineIdDelegate;
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
			Machines.Clear();
			VfsNodes.Clear();
		}

		public void SetMachines( IEnumerable<MachineDef> machines )
		{
			Machines.Clear();
			foreach( var mdef in machines )
			{
				var m = new TMachine();

				m.Id = mdef.Id;
				m.IP = mdef.IP;

				foreach( var s in mdef.FileShares )
				{
					if( !PathUtils.IsPathAbsolute(s.Path)  )
						throw new Exception($"Share part not absolute: {s}");

					m.Shares[s.Name] = s.Path;
				}

				Machines[mdef.Id] = m;
			}
		}

		public string GetMachineIP( string machineId )
		{
			string? ip = null;

			// find machine
			if( Machines.TryGetValue( machineId, out var m ) )
				ip = m.IP;
				
				// find machine IP
			if( string.IsNullOrEmpty( ip ) )
			{
				if( _machineIPDelegate != null && !string.IsNullOrEmpty( machineId ) )
				{
					ip = _machineIPDelegate( machineId );
				}
			}

			if( string.IsNullOrEmpty( ip ) )
				throw new Exception($"Could not find IP of machine {machineId}.");

			// remember the machine if not yet
			if( m is null )
			{
				m = new TMachine(); 
			}

			if( string.IsNullOrEmpty(m.IP) )
			{
				m.IP = ip;
			}

			return m.IP;
		}

		public string MakeUNC( string path, string? machineId, string whatFor )
		{
			// global paths are already UNC
			if ( string.IsNullOrEmpty(machineId) )
				return path;
				
			// find machine
			if ( !Machines.TryGetValue( machineId, out var m ) )
				throw new Exception($"Machine {machineId} not found for {whatFor}");

			var IP = GetMachineIP( machineId );

			foreach( var (shName, shPath) in m.Shares )
			{
				// get path relative to share
				if( path.StartsWith( shPath, StringComparison.OrdinalIgnoreCase ) )
				{
					var pathRelativeToShare = path.Substring( shPath.Length );
					return $"\\\\{IP}\\{shName}\\{pathRelativeToShare}";
				}
			}

			throw new Exception($"Can't construct UNC path, No file share matching {whatFor}");
		}
		
		public string MakeUNCIfNotLocal( string path, string? machineId, string whatFor )
		{
			if( _localMachineId != machineId )
			{
				return MakeUNC( path, machineId, whatFor );
			}
			else
			{
				return path;
			}
		}

		/// <summary>
		/// Returns direct path to the file, with all variables and file path resolution mechanism already evaluated.
		/// If we are on the machine where the file is, returns local path, otherwise returns remote path.
		/// </summary>
		/// <param name="fdef"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		string? ResolveFilePath( VfsNodeDef fdef, bool forceUNC )
		{
			// global file? must be UNC path already...
			if( string.IsNullOrEmpty( fdef.MachineId ) )
			{
				if( string.IsNullOrEmpty( fdef.Path ) )
				{
					throw new Exception($"FileDef path empty: {fdef}");
				}

				return fdef.Path;
			}

			if( string.IsNullOrEmpty( fdef.Path ) )
			{
				throw new Exception($"FileDef path empty: {fdef}");
			}

			bool isLocal = IsLocalMachine(fdef.MachineId);

			var path = fdef.Path;

			// expand variables in local context
			if( isLocal )
			{
				var vars = new Dictionary<string, string>();

				// for app-bound files, expand also local vars and define var for app working dir etc.
				if( fdef.MachineId == _localMachineId ) // are we the agent for this machine?
				{
					// KEEP IN SYNC WITH Launcher.cs
					vars["MACHINE_ID"] = _localMachineId;
					vars["MACHINE_IP"] = GetMachineIP( _localMachineId );
					vars["DIRIGENT_MACHINE_ID"] = _localMachineId;
					vars["DIRIGENT_MACHINE_IP"] = GetMachineIP( _localMachineId );
				
					if( !string.IsNullOrEmpty( fdef.AppId ) )
					{
						var appDef = _ctrl.GetAppDef( new AppIdTuple( fdef.MachineId, fdef.AppId ) );
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

				path = Tools.ExpandEnvAndInternalVars( path, vars );

				if( !PathUtils.IsPathAbsolute( path ) )
				{
					path = Path.Combine( _rootForRelativePaths, path );
				}
			}

			// if the file on local machine, return local path
			if( isLocal && !forceUNC )
			{
				return path;
			}


			// construct UNC path using file shares defined for machine

			var machineId = isLocal ? _localMachineId : fdef.MachineId;

			return MakeUNC( path, machineId, $"FileDef {fdef}" );
		}

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
			if( clientId == _localMachineId )
				return true;

			// try compare IP addresses
			var clientIP = _machineIPDelegate( clientId );
			if( clientIP is null )
				return false;

			var ourIP = _machineIPDelegate( _localMachineId );
			if( ourIP is null )
				return false;

			if( clientIP == ourIP )
				return true;

			return false;
		}

		/// <summary>
		/// Converts given VfsNode into a tree of virtual folders containing links to physical files.
		/// Resolves all links, scans the folders (remembering the contained files and subfolders if requested), expands variables.
		/// File paths returned are resolved from the perspective of the local machine - remote paths are UNC, variables expanded to values found on the remote machines.
		/// </summary>
		/// <param name="def">Root node of what to resolve</param>
		/// <param name="forceUNC">If true, all paths will be UNC, even if they are on the local machine</param>
		/// <param name="includeContent">If true, will include content of folders, otherwise will just include the folders themselves</param>
		/// <returns>
		///  Folders - VFolder will have just the Title (vfolder name).
		/// Files will have the Title and Path (link to physical file).
		/// </returns>
		public async Task<VfsNodeDef?> ResolveAsync( IDirigAsync iDirig, VfsNodeDef nodeDef, bool forceUNC, bool includeContent, List<Guid>? usedGuids )
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
				if( !string.IsNullOrEmpty(nodeDef.MachineId) &&  _machineIPDelegate( nodeDef.MachineId ) is null )
					throw new Exception($"Machine {nodeDef.MachineId} not connected.");
					
				// await script	to resolve remotely
				var args = new Scripts.BuiltIn.ResolveVfsPath.TArgs
				{
					VfsNode = nodeDef,
					ForceUNC = forceUNC,
					IncludeContent = includeContent
				};

				var result = await iDirig.RunScriptAsync<Scripts.BuiltIn.ResolveVfsPath.TArgs, Scripts.BuiltIn.ResolveVfsPath.TResult>(
						nodeDef.MachineId ?? "",
						Scripts.BuiltIn.ResolveVfsPath._Name,
						"",	// sourceCode
						args,
						$"Resolve {nodeDef.Xml}",
						out var instance
					);

				return result!.VfsNode!;

			}

			// from here on, we are on local machine (or master)

			if( nodeDef is FileDef fileDef )
			{
				return ResolveFileDef( forceUNC, fileDef );
			}
			else
			if( nodeDef is FileRef fref )
			{
				return await ResolveFileRef( iDirig, forceUNC, includeContent, usedGuids, fref );
			}
			else
			if (nodeDef is VFolderDef vfolderDef)
			{
				return await ResolveVFolder( iDirig, vfolderDef, forceUNC, usedGuids );
			}
			else
			if( nodeDef is FolderDef folderDef )
			{
				return ResolveFolder( folderDef, forceUNC, includeContent );
			}
			else
			if( nodeDef is FilePackageDef fpdef )
			{
				return await ResolveVFolder( iDirig, fpdef, forceUNC, usedGuids );
			}
			else
			{
				throw new Exception( $"Unknown VfsNodeDef type: {nodeDef}" );
			}
		}

		private async Task<VfsNodeDef?> ResolveFileRef( IDirigAsync iDirig, bool forceUNC, bool includeContent, List<Guid>? usedGuids, FileRef fref )
		{
			var defs = FindById( fref.Id, fref.MachineId, fref.AppId );

			// remove reference to self
			defs.RemoveAll( x => x.Guid == fref.Guid );

			if( defs.Count == 0 )
				return null;

			if ( defs.Count == 1 )
				return await ResolveAsync( iDirig, defs[0], forceUNC, includeContent, usedGuids );

			{
				var pack = new VFolderDef();
				pack.Title = fref.Title;
				if ( string.IsNullOrEmpty(pack.Title) ) pack.Title = fref.Id;
				if( string.IsNullOrEmpty(pack.Title) ) pack.Title = fref.Guid.ToString();
				foreach( var def in defs )
				{
					var resolved = await ResolveAsync( iDirig, def, forceUNC, includeContent, usedGuids );
					if( resolved is not null )
						pack.Children.Add( resolved );
				}
				return pack;
			}
				
			
		}

		private VfsNodeDef? ResolveFileDef( bool forceUNC, FileDef fileDef )
		{
			if (string.IsNullOrEmpty( fileDef.Path )) throw new Exception( $"FileDef.Path is empty. {fileDef.Xml}" );


			//if( fileDef.Path.Contains('%') )

			if (string.IsNullOrEmpty( fileDef.Filter ))
			{
				var r = EmptyFrom<ResolvedVfsNodeDef>( fileDef );
				r.IsContainer = false;
				r.Guid = fileDef.Guid;
				r.Path = ResolveFilePath( fileDef, forceUNC );
				if( r.Path is null ) return null;
				return r;
			}

			// newest file(s) from folder?
			//  - if just one file is requested, return one single FileDef or null
			//  - if multiple files allowed, return VFolder
			if (fileDef.Filter.Equals( "newest", StringComparison.OrdinalIgnoreCase ))
			{
				var folder = ResolveFilePath( fileDef, forceUNC );
				if( folder is null )
					return null;

				if (string.IsNullOrEmpty( fileDef.Xml )) throw new Exception( $"FileDef.Xml is empty. {fileDef.Xml}" );
				var xml = XElement.Parse( fileDef.Xml );

				string mask = X.getStringAttr( xml, "Mask", "*.*" );
				int maxFiles = X.getIntAttr( xml, "MaxFiles", 1 ); // by default a single file only
				if (maxFiles < 1) maxFiles = 1;
				double maxSeconds = X.getDoubleAttr( xml, "MaxSeconds", double.MaxValue ); // by default whatever age

				var newestFiles = GetNewestFilesInFolder( folder, mask, maxFiles, maxSeconds );

				// if just one single file requested, return FileDef
				if( maxFiles <= 1 )
				{
					if( newestFiles.Count == 0 )
					{
						return null;
					}
					else
					{
						var r = EmptyFrom<FileDef>( fileDef );
						r.Guid = fileDef.Guid;
						r.Path = newestFiles[0];
						return r;
					}
				}
				else
				// if more files possible, put them in VFolder
				{
					var pack = EmptyFrom<VFolderDef>( fileDef );
					if( string.IsNullOrEmpty(pack.Title) ) pack.Title = pack.Id;
					if( string.IsNullOrEmpty(pack.Title) ) pack.Title = pack.Guid.ToString();
					foreach( var fpath in newestFiles )
					{
						var r = EmptyFrom<FileDef>( fileDef );
						r.Guid = fileDef.Guid;
						r.Path = fpath;
						pack.Children.Add( r );
					}
					return pack;
				}
			}

			throw new Exception( $"Unsupported filter. {fileDef.Xml}" );
		}

		async Task<VfsNodeDef> ResolveVFolder( IDirigAsync iDirig, VfsNodeDef folderDef, bool forceUNC, List<Guid>? usedGuids )
		{
			var rootNode = EmptyFrom<ResolvedVfsNodeDef>( folderDef ); // this produces Iscontainer=false (ResolvedVfsNode does not say if it is a container or not)
			rootNode.IsContainer = true;

			// FIXME: group children by machineId, resolve whole group by single remote script call
			foreach ( var child in folderDef.Children )
			{
				var resolved = await ResolveAsync( iDirig, child, forceUNC, true, usedGuids );
				if( resolved is not null )
				{
					rootNode.Children.Add( resolved );
				}
			}

			return rootNode;
		}

		VfsNodeDef? ResolveFolder( FolderDef folderDef, bool forceUNC, bool includeContent )
		{
			var rootNode = EmptyFrom<VFolderDef>( folderDef );
			rootNode.Path = ResolveFilePath( folderDef, forceUNC );
			
			if( includeContent )
			{
				// FIXME:
				// traverse all files & folders 
				// filter by glob-style mask
				// convert into vfs tree structure
				var folderName = ResolveFilePath( folderDef, forceUNC );
				if( string.IsNullOrEmpty(folderName) )
					return null;
				// ....

				var mask = folderDef.Mask;
				if( string.IsNullOrEmpty(mask) ) mask = "*.*"; //throw new Exception($"No file mask given in '{pathWithMask}'");

				try
				{
					var dirs = FindDirectories( folderName );
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
						var vfsFolder = ResolveFolder( dirDef, forceUNC, includeContent );
						if( vfsFolder is not null )
						{
							rootNode.Children.Add( vfsFolder );
						}
					}
				}
				catch( Exception ex ) // folder not exists or not accessible?
				{
					log.Debug($"ResolveFolder failed: {folderDef} Error: {ex.Message}");
					return null;
				}

				try
				{
					var files = FindMatchingFileInfos( folderName, mask, false );
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
					log.Debug($"ResolveFolder failed: {folderDef} Error: {ex.Message}");
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

