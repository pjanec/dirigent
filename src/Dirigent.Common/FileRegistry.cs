
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;
using System.IO;
using System.Xml.Linq;
using System.Threading;

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

		public FileRegistry( string localMachineId, GetMachineIPDelegate machineIdDelegate )
		{
			_localMachineId = localMachineId;
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

		public string GetMachineIP( string machineId, string whatFor )
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
		/// Returns direct path to the file, with all variables and file path resolution mechanism already evaluated.
		/// If we are on the machine where the file is, returns local path, otherwise returns remote path.
		/// </summary>
		/// <param name="fdef"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public string GetFilePath( VfsNodeDef fdef )
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

			// if the file on local machine, return local path
			if (fdef.MachineId == _localMachineId)
			{
				// TODO: for app-bound files, expand also local vars and define var for app working dir etc.
				var expanded = Tools.ExpandEnvVars( fdef.Path );
				return expanded;
			}

			// construct UNC path using file shares defined for machine

			// find machine
			if ( !Machines.TryGetValue( fdef.MachineId, out var m ) )
				throw new Exception($"Machine {fdef.MachineId} not found for FileDef {fdef}");

			var IP = GetMachineIP( fdef.MachineId, $"FileDef {fdef}" );

			foreach( var (shName, shPath) in m.Shares )
			{
				// get path relative to share
				if( fdef.Path.StartsWith( shPath, StringComparison.OrdinalIgnoreCase ) )
				{
					var pathRelativeToShare = fdef.Path.Substring( shPath.Length );
					return $"\\\\{IP}\\{shName}\\{pathRelativeToShare}";
				}
			}

			throw new Exception($"No file share matching FileDef {fdef}, can't construct UNC path");
		}

		VfsNodeDef? FindById( string Id, string? machineId, string? appId )
		{
			foreach( var node in VfsNodes.Values )
			{
				if(	node.Id == Id &&
					node.MachineId == machineId &&
					node.AppId == appId )
					return node;
			}
			return null;
		}

		static T EmptyFrom<T>( VfsNodeDef x ) where T: VfsNodeDef, new()
		{
			return new T {
				Guid = x.Guid,
				Id = x.Id,
				Title = x.Title,
				MachineId = x.MachineId,
			};
		}


		/// <summary>
		/// Resolve all links, expands all widcards etc. Produces a tree of VfsNodes.
		/// </summary>
		/// <param name="machineId">Tree of definitions (descendants of VfsNodeDef)</param>
		/// <param name="defs">Tree of definitions (descendants of VfsNodeDef)</param>
		/// <returns>A tree of plain VfsNodeDef instances containing just files (leave nodes) and folders (intermediate nodes).
		/// Folders having just Title, files having Title and Path.
		/// Path is resolved from the perspective of the local machine - remote paths are UNC.
		/// </returns>
		public async Task<VfsNodeDef> ResolveAsync( IDirigAsync iDirig, VfsNodeDef nodeDef, List<Guid>? usedGuids, CancellationToken ct, int timeoutMs=-1 )
		{
			if (nodeDef is null)
				throw new ArgumentNullException( nameof( nodeDef ) );
				
			if (usedGuids == null) usedGuids = new List<Guid>();
			if (usedGuids.Contains( nodeDef.Guid ))
				throw new Exception( $"Circular reference in VFS tree: {nodeDef}" );
			
			usedGuids.Add( nodeDef.Guid );

			// non-local stuff to be always resolved on machine where local - via remote script call
			if( nodeDef.MachineId != _localMachineId )
			{
				// check if required machine is available
				if( !string.IsNullOrEmpty(nodeDef.MachineId) &&  _machineIPDelegate( nodeDef.MachineId ) is null )
					throw new Exception($"Machine {nodeDef.MachineId} not connected.");
				// await script	to resolve remotely
				var args = new Scripts.BuiltIn.ResolveVfsPath.TArgs
				{
					VfsNode = nodeDef
				};

				var result = await iDirig.RunScriptAndWaitAsync<Scripts.BuiltIn.ResolveVfsPath.TArgs, Scripts.BuiltIn.ResolveVfsPath.TResult>(
					nodeDef.MachineId ?? "",
					Scripts.BuiltIn.ResolveVfsPath._Name,
					"",	// sourceCode
					args,
					$"Resolve {nodeDef.Xml}",
					ct,
					timeoutMs
				);

				return result!.VfsNode!;

			}

			// from here on, we are on local machine

			if( nodeDef is FileDef fileDef )
			{
				if( string.IsNullOrEmpty(fileDef.Path) ) throw new Exception($"FileDef.Path is empty. {fileDef.Xml}");


				//if( fileDef.Path.Contains('%') )

				if( string.IsNullOrEmpty(fileDef.Filter ) )
				{
					var r = EmptyFrom<VfsNodeDef>( fileDef );
					r.Path = GetFilePath( fileDef );
					return r;
				}

				if( fileDef.Filter.Equals( "newest", StringComparison.OrdinalIgnoreCase ) )
				{
					var folder = GetFilePath( fileDef );

					if( string.IsNullOrEmpty(fileDef.Xml) ) throw new Exception($"FileDef.Xml is empty. {fileDef.Xml}");
					var xel = XElement.Parse(fileDef.Xml);
					string? mask = xel.Attribute( "Mask" )?.Value;
					if( mask is null ) mask = "*.*";

					var r = EmptyFrom<FileDef>( fileDef );
					r.Path = GetNewestFileInFolder( folder, mask );
					return r;
				}

				throw new Exception($"Unsupported filter. {fileDef.Xml}");
			}
			else
			if( nodeDef is FileRef fref )
			{
				var def = FindById( fref.Id, fref.MachineId, fref.AppId ) as FileDef;
				if( def is null )
					throw new Exception( $"{fref} points to non-existing FileDef" );

				return await ResolveAsync( iDirig, def, usedGuids, ct, timeoutMs );
			}
			else
			if (nodeDef is VFolderDef vfolderDef)
			{
				var ret = new VfsNodeDef
				{
					IsContainer = true
				};

				// FIXME: group children by machineId, resolve whole group by single remote script call
				foreach( var child in vfolderDef.Children )
				{
					var resolved = await ResolveAsync( iDirig, child, usedGuids, ct, timeoutMs );
					ret.Children.Add( resolved );
				}

				return ret;
			}
			else
			if( nodeDef is FolderDef folderDef )
			{
				return ResolveFolder( folderDef, false ); // just that one folder, not the content
			}
			else
			if( nodeDef is FilePackageRef fpref )
			{
				var def = FindById( fpref.Id, fpref.MachineId, fpref.AppId ) as FilePackageDef;
				if( def is null )
					throw new Exception( $"{fpref} points to non-existing FilePackage" );

				return await ResolveAsync( iDirig, def, usedGuids, ct, timeoutMs );
			}
			else
			{
				throw new Exception( $"Unknown VfsNodeDef type: {nodeDef}" );
			}
		}

		VfsNodeDef ResolveFolder( FolderDef folderDef, bool includeContent )
		{
			var rootNode = EmptyFrom<FolderDef>( folderDef );
			rootNode.IsContainer = true;
			rootNode.Path = GetFilePath( folderDef );
			
			if( includeContent )
			{
				// FIXME:
				// traverse all files & folders 
				// filter by glob-style mask
				// convert into vfs tree structure
				var folderName = GetFilePath( folderDef );
				// ....

				var mask = folderDef.Mask;
				if( string.IsNullOrEmpty(mask) ) mask = "*.*"; //throw new Exception($"No file mask given in '{pathWithMask}'");

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
					var vfsFolder = ResolveFolder( dirDef, includeContent );
					rootNode.Children.Add( vfsFolder );
				}

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
			
			return rootNode;
		}

		string? GetNewestFileInFolder( string folderName, string mask )
		{
			var files = FindMatchingFileInfos( folderName, mask, false );
			var newest = GetNewest( files );
			return newest;
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

