
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;
using System.IO;

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
			// find machine
			if( !Machines.TryGetValue( machineId, out var m ) )
				throw new Exception($"Machine {machineId} not found for {whatFor}");

			// find machine IP
			if( string.IsNullOrEmpty( m.IP ) )
			{
				if( _machineIPDelegate != null && !string.IsNullOrEmpty( machineId ) )
				{
					m.IP = _machineIPDelegate( machineId );
				}

				if( string.IsNullOrEmpty( m.IP ) )
					throw new Exception($"Could not find IP of machine {machineId}");
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
				return fdef.Path;
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

		/// <summary>
		/// Resolve all links, expands all widcards etc. Produces a tree of VfsNodes.
		/// </summary>
		/// <param name="machineId">Tree of definitions (descendants of VfsNodeDef)</param>
		/// <param name="defs">Tree of definitions (descendants of VfsNodeDef)</param>
		/// <returns>A tree of plain VfsNodeDef instances containing just files (leave nodes) and folders (intermediate nodes).
		/// Folders having just Title, files having Title and Path.
		/// Path is resolved from the perspective of the local machine - remote paths are UNC.
		/// </returns>
		public VfsNodeDef Resolve( VfsNodeDef nodeDef, List<Guid>? usedGuids )
		{
			if (usedGuids == null) usedGuids = new List<Guid>();
			if (usedGuids.Contains( nodeDef.Guid ))
				throw new Exception( $"Circular reference in VFS tree: {nodeDef}" );
			
			usedGuids.Add( nodeDef.Guid );

			if ( nodeDef is FileDef fileDef )
			{
				return new VfsNodeDef
				{
					Title = !string.IsNullOrEmpty(fileDef.Title) ? fileDef.Title : fileDef.Id,
					Path = GetFilePath( fileDef )
				};
			}
			else
			if( nodeDef is FileRef fref )
			{
				var def = FindById( fref.Id, fref.MachineId, fref.AppId ) as FileDef;
				if( def is null )
					throw new Exception( $"{fref} points to non-existing FileDef" );

				return Resolve( def, usedGuids );
			}
			else
			if (nodeDef is VFolderDef vfolderDef)
			{
				var ret = new VfsNodeDef
				{
					IsContainer = true
				};

				foreach( var child in vfolderDef.Children )
				{
					var resolved = Resolve( child, usedGuids );
					ret.Children.Add( resolved );
				}

				return ret;
			}
			else
			if( nodeDef is FolderDef folderDef )
			{
				return ResolveFolder( folderDef );
			}
			else
			if( nodeDef is FilePackageRef fpref )
			{
				var def = FindById( fpref.Id, fpref.MachineId, fpref.AppId ) as FilePackageDef;
				if( def is null )
					throw new Exception( $"{fpref} points to non-existing FilePackage" );

				return Resolve( def, usedGuids );
			}
			else
			{
				throw new Exception( $"Unknown VfsNodeDef type: {nodeDef}" );
			}
		}

		VfsNodeDef ResolveFolder( FolderDef folderDef )
		{
			var rootNode = new VfsNodeDef
			{
				IsContainer = true,
				Title = !string.IsNullOrEmpty(folderDef.Title) ? folderDef.Title : folderDef.Id,
			};
			
			// FIXME:
			// traverse all files & folders 
			// filter by glob-style mask
			// convert into vfs tree structure
			var basePath = GetFilePath( folderDef );
			// ....

			return rootNode;
		}
	}
}

