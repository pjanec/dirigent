
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;

namespace Dirigent
{

	public class FilePackage
	{
		public FilePackageDef Def;
		public string? Id => Def.Id;
		public string? MachineId => Def.MachineId;
		public string? AppId => Def.AppId;
		public List<FileDef> Files = new List<FileDef>();

		public FilePackage( FilePackageDef def )
		{
			Def = def;
		}
	}


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

		public Dictionary<Guid, FileDef> Files { get; private set; } = new Dictionary<Guid, FileDef>();
		public Dictionary<Guid, FilePackage> Packages { get; private set; } = new Dictionary<Guid, FilePackage>();
		public Dictionary<string, TMachine> Machines { get; private set; } = new Dictionary<string, TMachine>();



		public FileRegistry( GetMachineIPDelegate machineIdDelegate )
		{
			_machineIPDelegate = machineIdDelegate;
		}
		
		public FileDef? GetFileDef( Guid guid )
		{
			if( Files.TryGetValue( guid, out var fdef ) ) return fdef;
			return null;
		}

		public IEnumerable<FileDef> GetAllFileDefs() => Files.Values;

		public FilePackage? GetFilePackage( Guid guid )
		{
			if( Packages.TryGetValue( guid, out var pkg ) ) return pkg;
			return null;
		}

		public IEnumerable<FilePackage> GetAllFilePackages() => Packages.Values;


		public void SetFiles( IEnumerable<FileDef> fileDefs, IEnumerable<FilePackageDef> pkgDefs )
		{
			Files.Clear();
			foreach( var file in fileDefs )
			{
				Files[file.Guid] = file;
			}

			PackageDefs = new List<FilePackageDef>( pkgDefs );
			
			Packages.Clear();
			foreach( var pkg in pkgDefs )
			{
				var p = new FilePackage(pkg);
				foreach( var fileGuid in pkg.Files )
				{
					if( Files.TryGetValue( fileGuid, out var fdef ) )
					{
						p.Files.Add( fdef );
					}
					else throw new Exception( $"Package {pkg}: file reference invalid!");
				}
				Packages[pkg.Guid] = p;
			}
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

		
		public string GetFileUNCPath( FileDef fdef )
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

			// find machine
			if( !Machines.TryGetValue( fdef.MachineId, out var m ) )
				throw new Exception($"Machine {fdef.MachineId} not found for FileDef {fdef}");

			// find machine IP
			if( string.IsNullOrEmpty( m.IP ) )
			{
				if( _machineIPDelegate != null && !string.IsNullOrEmpty( fdef.MachineId ) )
				{
					m.IP = _machineIPDelegate( fdef.MachineId );
				}

				if( string.IsNullOrEmpty( m.IP ) )
					throw new Exception($"Could not find IP of machine {fdef.MachineId}");
			}

			foreach( var (shName, shPath) in m.Shares )
			{
				// get path relative to share
				if( fdef.Path.StartsWith( shPath, StringComparison.OrdinalIgnoreCase ) )
				{
					var pathRelativeToShare = fdef.Path.Substring( shPath.Length );
					return $"\\{m.IP}\\{shName}\\{pathRelativeToShare}";
				}
			}

			throw new Exception($"No file share matching FileDef {fdef}");
		}
	}
}

