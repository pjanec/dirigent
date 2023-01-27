
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

namespace Dirigent
{

	public enum EPathType
	{
		Unknown,
		Auto,	// first possible in this order: local, unc, ssh
		Local,  // C:\path
		UNC,    // \\server\share\path or 
		SSH,    // sftp://user@host:port/path or scp://user@host:port/path
		LocalOrUNC, // can't be used as 'from' type, only as 'to' type
	}


	/// <summary>
	/// Translates paths to be accessible from local machine
	/// </summary>
	public class PathPerspectivizer
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		
		// these should be set from outside
		
		public bool IsConnectedViaSSH { get; set; }

		string _localMachineId; // null or empty = not on any known local machine

		MachineRegistry _machineRegistry;

		
		public PathPerspectivizer( string localMachineId, MachineRegistry machineRegistry )
		{
			_localMachineId = localMachineId;
			_machineRegistry = machineRegistry;
		}
		

		// converts given path to local machine perspective
		// returns null if path can't be translated
		public string TranslatePath( string? path, string? machineId, EPathType to )
		{
			if( string.IsNullOrEmpty( path ) )
			{
				throw new Exception($"Can't translate null path!");
			}

			var from = GetPathType( path );
			if( from == EPathType.Unknown || to == EPathType.Unknown )
			{
				throw new Exception( $"Unknown path type: {path}" );
			}

			if( from == EPathType.UNC )
			{
				if (to == EPathType.UNC )
				{
					return path;
				}
				throw new Exception( $"Can't translate UNC path to {to}: {path}@{machineId}" );
			}

			if( from == EPathType.Local )
			{
				if( to == EPathType.Local )
				{
					if( machineId == _localMachineId )
					{
						return path;
					}
					else throw new Exception( $"Can't get local path '{path}' for another machine {machineId}." );
				}

				if( to == EPathType.LocalOrUNC )
				{
					if( machineId == _localMachineId )
					{
						return path;
					}
					to = EPathType.UNC;
				}

				if( to == EPathType.UNC ) // use <Share/> if we know the machine
				{
					return MakeUNC( path, machineId );
				}

				if( to == EPathType.SSH ) // use <SshPaths/> if we know the machine
				{
					return MakeSsh( path, machineId );
				}

				throw new Exception($"Can't translate from {from} to {to}: {path}@{machineId}");
			}

			throw new Exception($"Can't translate {from} to {to}: {path}@{machineId}");
		}


		// translate the Path of the given VfsNodeDef its children
		public void PerspectivizePath( VfsNodeDef vfsNode, EPathType to=EPathType.Auto )
		{
			if( vfsNode.IsContainer )
			{
				foreach (var child in vfsNode.Children)
				{
					PerspectivizePath( child, to );
				}
			}
			else
			{
				if( !string.IsNullOrEmpty(vfsNode.Path) )
				{
					vfsNode.Path = TranslatePath( vfsNode.Path, vfsNode.MachineId, to );
				}
			}
		}

		public Task PerspectivizePathAsync( VfsNodeDef nodeDef, EPathType to )
		{
			PerspectivizePath( nodeDef, to );
			return Task.CompletedTask;
		}

		public static EPathType GetPathType( string? path )
		{
			if( path is null )
				return EPathType.Unknown;
				
			if (path.StartsWith( @"\\", StringComparison.Ordinal ))
			{
				return EPathType.UNC;
			}
			else if (path.StartsWith( @"sftp://", StringComparison.OrdinalIgnoreCase ))
			{
				return EPathType.SSH;
			}
			else if (path.StartsWith( @"scp://", StringComparison.OrdinalIgnoreCase ))
			{
				return EPathType.SSH;
			}
			else if (path.StartsWith( @"winscp-sftp://", StringComparison.OrdinalIgnoreCase ))
			{
				return EPathType.SSH;
			}
			else if (path.StartsWith( @"winscp-scp://", StringComparison.OrdinalIgnoreCase ))
			{
				return EPathType.SSH;
			}
			else if (path.Length >= 3 && Char.IsLetter( path[0] ) && path[1] == ':' && (path[2] == '\\' || path[2] == '/') )
			{
				return EPathType.Local;
			}
			else
			{
				return EPathType.Unknown;
			}
		}

		public string MakeUNC( string path, string? machineId )
		{
			// global paths are already UNC
			if ( string.IsNullOrEmpty(machineId) )
			{
				return path;
			}
					
			// find machine
			if( !_machineRegistry.Machines.TryGetValue( machineId, out var m ) )
				throw new Exception($"Machine {machineId} not found for {path}@{machineId}");

			// find machine IP
			var IP = _machineRegistry.GetMachineIP( machineId );

			foreach( var rec in m.Def.FileShares )
			{
				// get path relative to share
				if( path.StartsWith( rec.Path, StringComparison.OrdinalIgnoreCase ) )
				{
					var pathRelativeToShare = path.Substring( rec.Path.Length );
					return $"\\\\{IP}\\{rec.Name}\\{pathRelativeToShare}";
				}
			}

			throw new Exception($"Can't construct UNC path, no <Share/> record matching {path}@{machineId}");
		}

		public string MakeUNCIfNotLocal( string path, string? machineId )
		{
			if( _localMachineId != machineId )
			{
				return MakeUNC( path, machineId );
			}
			else
			{
				return path;
			}
		}

		public string MakeSsh( string path, string? machineId )
		{
			if( machineId is null )
				throw new Exception($"Null machine id");

			// find machine IP
			var IP = _machineRegistry.GetMachineIP( machineId );

			if ( !_machineRegistry.Machines.TryGetValue( machineId, out var m ) )
				throw new Exception($"Machine {machineId} not found for {path}@{machineId}");

						
			foreach( var rec in m.Def.SshUrls )
			{
				if( path.StartsWith( rec.Path, StringComparison.OrdinalIgnoreCase ) )
				{
					var pathRelativeToShare = path.Substring( rec.Path.Length ).Replace("\\", "/");

					var vars = new Dictionary<string,string>();
					vars["GW_USERNAME"] = "";  // from gateway session
					vars["GW_PASSWORD"] = "";
					vars["GW_IP"] = "";
					vars["GW_PORT"] = "";
					vars["MACHINE_USERNAME"] = ""; // from machine
					vars["MACHINE_PASSWORD"] = "";
					vars["MACHINE_IP"] = IP;

					var urlPrefix = Tools.ExpandEnvAndInternalVars( rec.UrlPrefix , vars);
					return $"{urlPrefix}/{pathRelativeToShare}";
				}
			}

			throw new Exception($"Can't construct SSH path, no <SshPath/> record matching {path}@{machineId}");
		}
	}
}

