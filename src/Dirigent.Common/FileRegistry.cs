
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

		string? _downloadFolder;

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
		
		/// <param name="downloadFolder">
		/// What %DOWNLOADS% expands to on this machine. Empty or null means the download folder of
		/// the user this process runs as.
		/// </param>
		public FileRegistry( IDirig ctrl, string localMachineId, string rootForRelativePaths, GetMachineIPDelegate machineIdDelegate, string? downloadFolder = null )
		{
			_ctrl = ctrl;
			_localMachineId = localMachineId;
			_rootForRelativePaths = rootForRelativePaths;
			_downloadFolder = downloadFolder;
			_machineIPDelegate = machineIdDelegate;
		}
		
		public VfsNodeDef? GetVfsNodeDef( Guid guid )
		{
			if( VfsNodes.TryGetValue( guid, out var def ) ) return def;
			return null;
		}

		public IEnumerable<VfsNodeDef> GetAllVfsNodesDef() => VfsNodes.Values;


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

		/// <summary>
		/// Turns a path local to the given machine into a UNC path leading through one of that
		/// machine's file shares.
		/// </summary>
		/// <remarks>
		/// The share covering the path most specifically wins, the way a mount table works, so that
		/// a share dedicated to a subtree (D:\Logs) is preferred over one covering the whole drive
		/// (D:\). Without that the winner would depend on the order the shares happen to be stored
		/// in, and a share declared for a particular folder - typically the one with the permissions
		/// set up for it - could be bypassed.
		/// </remarks>
		public string MakeUNC( string path, string? machineId, string whatFor )
		{
			// global paths are already UNC
			if ( string.IsNullOrEmpty(machineId) )
				return path;

			// find machine
			if ( !Machines.TryGetValue( machineId, out var m ) )
				throw new Exception($"Machine {machineId} not found for {whatFor}");

			var IP = GetMachineIP( machineId );

			string? bestName = null;
			string? bestRoot = null;

			foreach( var (shName, shPath) in m.Shares )
			{
				var root = ShareRootCoveringPath( shPath, path );
				if( root is null )
					continue;

				if( bestRoot is null
					|| root.Length > bestRoot.Length
					// two shares of the same folder: pick by name, just to stay predictable
					|| ( root.Length == bestRoot.Length && string.CompareOrdinal( shName, bestName ) < 0 ) )
				{
					bestName = shName;
					bestRoot = root;
				}
			}

			if( bestRoot is null )
				throw new Exception($"Can't construct UNC path, No file share matching {whatFor}");

			var pathRelativeToShare = path.Substring( bestRoot.Length ).TrimStart( '\\', '/' );

			return string.IsNullOrEmpty( pathRelativeToShare )
					? $"\\\\{IP}\\{bestName}"
					: $"\\\\{IP}\\{bestName}\\{pathRelativeToShare}";
		}

		/// <summary>
		/// The share's folder, without a trailing separator, if the share contains the given path;
		/// null if it does not.
		/// </summary>
		/// <remarks>
		/// The path must continue at a folder boundary, otherwise a share at "D:\Logs" would claim
		/// "D:\LogsBackup\a.txt" and silently produce a UNC path to a different file.
		/// </remarks>
		static string? ShareRootCoveringPath( string sharePath, string path )
		{
			var shareRoot = sharePath.TrimEnd( '\\', '/' );

			if( !path.StartsWith( shareRoot, StringComparison.OrdinalIgnoreCase ) )
				return null;

			if( path.Length == shareRoot.Length ) // the share's own folder
				return shareRoot;

			var next = path[shareRoot.Length];
			return ( next == '\\' || next == '/' ) ? shareRoot : null;
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

				// the download folder of this machine; configurable so that it need not be the
				// real user folder (a test run must not litter it)
				vars["DOWNLOADS"] = string.IsNullOrEmpty( _downloadFolder )
										? Tools.GetDownloadFolderPath()
										: _downloadFolder;

				// for app-bound files, expand also local vars and define var for app working dir etc.
				if( fdef.MachineId == _localMachineId ) // are we the agent for this machine?
				{
					// KEEP IN SYNC WITH Launcher.cs
					vars["MACHINE_ID"] = _localMachineId;
					vars["DIRIGENT_MACHINE_ID"] = _localMachineId;

					// The IP is only known once the machine definitions have arrived, and asking
					// for it throws when they have not. Resolving a path that does not mention the
					// IP at all must not fail for that reason, so only look it up when it is used.
					if( MentionsMachineIP( path ) )
					{
						var machineIP = GetMachineIP( _localMachineId );
						vars["MACHINE_IP"] = machineIP;
						vars["DIRIGENT_MACHINE_IP"] = machineIP;
					}
				
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

		/// <summary>
		/// Whether the path uses the machine IP variable, in any of its spellings.
		/// </summary>
		static bool MentionsMachineIP( string path )
			=> path.IndexOf( "MACHINE_IP", StringComparison.OrdinalIgnoreCase ) >= 0;

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
			r.AppId = x.AppId; // kept so that the actions can tell which app the resolved file belongs to
			r.TailBytes = x.TailBytes; // the download needs it, and only the definition knows it
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
			// (a FileRef is the exception: it is resolved here, by looking it up in the registry we
			// hold a copy of. Its machine id is a filter for that lookup and may be a wildcard, so
			// it is not the name of a machine to send the reference to. Whatever the lookup finds
			// then gets resolved on its own machine.)
			if( nodeDef is not FileRef
				&& !string.IsNullOrEmpty(nodeDef.MachineId) // global resources are machine independent - can be resolved on any machine
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

			if( string.IsNullOrEmpty( rootNode.Path ) )
				return null;

			if( !includeContent )
				return rootNode;

			FileScan.Result scan;
			try
			{
				scan = FileScan.FindMatchingFiles(
					rootNode.Path,
					folderDef.Mask,
					folderDef.MaxSeconds,
					folderDef.MaxFiles,
					folderDef.MaxTotalBytes,
					recursive: true,
					tailBytes: folderDef.TailBytes
				);
			}
			catch( Exception ex ) // folder not exists or not accessible?
			{
				log.Debug($"ResolveFolder failed: {folderDef} Error: {ex.Message}");
				return null;
			}

			// what the size budget pushed out is worth saying out loud - the user asked for those files
			if( scan.Skipped.Count > 0 )
			{
				var note = $"{scan.Skipped.Count} file(s) of '{rootNode.Path}' left out, over the "
						+ $"{folderDef.MaxTotalBytes} byte limit of node '{folderDef.Id}': "
						+ string.Join( ", ", scan.Skipped.Take( 20 ).Select( s => $"{s.RelPath} ({s.Bytes} B)" ) )
						+ ( scan.Skipped.Count > 20 ? ", ..." : "" );

				log.Warn( note );
				( rootNode.Notes ??= new List<string>() ).Add( note );
			}

			// build the tree of virtual subfolders mirroring the location of the files within the scanned folder
			foreach( var (relPath, info) in scan.Files )
			{
				var parent = GetOrCreateSubFolder( rootNode, System.IO.Path.GetDirectoryName( relPath ), folderDef );

				parent.Children.Add(
					new FileDef
					{
						Path = info.FullName,
						MachineId = folderDef.MachineId,
						AppId = folderDef.AppId,
						IsContainer = false,
						Title = info.Name,
						TailBytes = folderDef.TailBytes, // a folder's setting applies to its files
					}
				);
			}

			return rootNode;
		}

		/// <summary>
		/// Finds (or creates) the chain of virtual subfolders for given folder path relative to the root node.
		/// Returns the deepest one, or the root node itself if the relative path is empty.
		/// </summary>
		static VfsNodeDef GetOrCreateSubFolder( VfsNodeDef rootNode, string? relDirPath, FolderDef folderDef )
		{
			var current = rootNode;

			if( string.IsNullOrEmpty( relDirPath ) )
				return current;

			var pathSoFar = rootNode.Path ?? string.Empty;

			foreach( var segment in relDirPath.Split( new char[]{'/','\\'}, StringSplitOptions.RemoveEmptyEntries ) )
			{
				pathSoFar = System.IO.Path.Combine( pathSoFar, segment );

				var subFolder = current.Children.Find(
					x => x.IsContainer && string.Equals( x.Title, segment, StringComparison.OrdinalIgnoreCase )
				);

				if( subFolder is null )
				{
					subFolder = new VFolderDef
					{
						Path = pathSoFar,
						MachineId = folderDef.MachineId,
						AppId = folderDef.AppId,
						IsContainer = true,
						Title = segment,
					};
					current.Children.Add( subFolder );
				}

				current = subFolder;
			}

			return current;
		}

		List<string> GetNewestFilesInFolder( string folderName, string mask, int maxFiles, double maxAgeSeconds )
		{
			// the mask of the 'Newest' filter applies to the files in the given folder only, never to subfolders
			var scan = FileScan.FindMatchingFiles( folderName, mask, maxAgeSeconds, maxFiles, 0, recursive: false );

			return ( from x in scan.Files select x.Info.FullName ).ToList();
		}

	}
}

