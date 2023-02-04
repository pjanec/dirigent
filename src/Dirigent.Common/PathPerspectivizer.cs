
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
		Local,  // C:\path
		UNC,    // \\server\share\path or 
		SSH,    // sftp://user@host:port/path or scp://user@host:port/path
	}

	public class SshSymLink
	{
		public string Name = ""; // what the link is called ("mylink1")
		public string TargetPath = ""; // where the link leads ("\\10.0.0.1\C\mydir")
		public string LocalPath = ""; // full path on the target machine ("C:\mydir")
		public string GatewayDir = ""; // full path to the folder where the link is stored on the gateway; in sftp compatible format ("/c/links/"); empty = failure; set when the links are created on the gw machine
	}

	public interface ISshStateProvider
	{
		bool IsConnected { get; }
		IDictionary<string,string> GetVariables( string machineId, string serviceName );
		IEnumerable<SshSymLink> GetSymLinks( string machineId );
	}

	/// <summary>
	/// Translates paths to be accessible from local machine
	/// </summary>
	public class PathPerspectivizer
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		
		// these should be set from outside
		
		string _localMachineId; // null or empty = not on any known local machine

		MachineRegistry _machineRegistry;


		public ISshStateProvider? SshStateProvider { get; set; }

		
		public PathPerspectivizer( string localMachineId, MachineRegistry machineRegistry )
		{
			_localMachineId = localMachineId;
			_machineRegistry = machineRegistry;
		}

		public static bool IsPathLocalOrUNC( string? path )
		{
			var type = GetPathType( path );
			return type == EPathType.Local || type == EPathType.UNC;
		}
		
		public static bool IsPathSsh( string? path )
		{
			return GetPathType(path) == EPathType.SSH;
		}

		bool IsConnectedViaSSH => SshStateProvider is null ? false : SshStateProvider.IsConnected;


		//// converts given path to local machine perspective
		//// returns null if path can't be translated
		//public string TranslatePath( string? path, string? machineId, EPathType to )
		//{
		//	if( string.IsNullOrEmpty( path ) )
		//	{
		//		throw new Exception($"Can't translate null path!");
		//	}

		//	var from = GetPathType( path );
		//	if( from == EPathType.Unknown || to == EPathType.Unknown )
		//	{
		//		throw new Exception( $"Unknown path type: {path}" );
		//	}

		//	if( from == EPathType.UNC )
		//	{
		//		if( to == EPathType.UNC )
		//		{
		//			return path;
		//		}
		//		{
		//			return path;
		//		}
		//		throw new Exception( $"Can't translate UNC path to {to}: {path}@{machineId}" );
		//	}

		//	if( from == EPathType.Local )
		//	{
		//		if( to == EPathType.Local )
		//		{
		//			if( machineId == _localMachineId )
		//			{
		//				return path;
		//			}
		//			else throw new Exception( $"Can't get local path '{path}' for another machine {machineId}." );
		//		}

		//		//if( to == EPathType.LocalOrUNC )
		//		//{
		//		//	if( machineId == _localMachineId )
		//		//	{
		//		//		return path;
		//		//	}
		//		//	to = EPathType.UNC;
		//		//}

		//		if( to == EPathType.UNC ) // use <Share/> if we know the machine
		//		{
		//			return MakeUNC( path, machineId );
		//		}

		//		if( to == EPathType.SSH ) // use <SshPaths/> if we know the machine
		//		{
		//			return MakeSsh( path, machineId );
		//		}

		//		throw new Exception($"Can't translate from {from} to {to}: {path}@{machineId}");
		//	}

		//	throw new Exception($"Can't translate {from} to {to}: {path}@{machineId}");
		//}


		// translate the Path of the given VfsNodeDef its children
		public void PerspectivizePath( VfsNodeDef vfsNode )
		{
			if( vfsNode.IsContainer )
			{
				foreach (var child in vfsNode.Children)
				{
					PerspectivizePath( child );
				}
			}
			else
			{
				if( !string.IsNullOrEmpty(vfsNode.Path) )
				{
					vfsNode.Path = PerspectivizePath( vfsNode.Path, vfsNode.MachineId );
				}
			}
		}

		string PerspectivizePath( string path, string? machineId )
		{
			// empty path stays empty no matter what
			if( string.IsNullOrEmpty( path ) )
			{
				return path;
			}

			var from = GetPathType( path );
			switch( from )
			{
				case EPathType.Local:
				{
					// if we go through SSH, always SSH
					if( IsConnectedViaSSH )
					{
						return MakeSsh( path, machineId );
					}
					return MakeUNCIfNotLocal( path, machineId );
				}

				case EPathType.UNC:
				{
					// UNC is fine as is unless we go through SSH
					if( !IsConnectedViaSSH )
					{
						return path;
					}
					throw new Exception( $"UNC path can't be used while connected via SSH" );
				}

				case EPathType.SSH:
				{
					// SSH path can't be simplified
					return path;
				}
			}

			throw new Exception( $"Failed to convert path '{path}@{machineId}' to the perspective of machine {_localMachineId}" );
		}
		


		public Task PerspectivizePathAsync( VfsNodeDef nodeDef )
		{
			PerspectivizePath( nodeDef );
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
				path = Path.GetFullPath( path ); // normalizes slashes
				var recPath = Path.GetFullPath(rec.Path);
				if( path.StartsWith( recPath, StringComparison.OrdinalIgnoreCase ) )
				{
					var pathRelativeToShare = path.Substring( recPath.Length );
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

			if (!IsConnectedViaSSH)
				throw new Exception( $"Not connected via SSH" );

			if (SshStateProvider is null)
				throw new Exception( $"No SshStateProvider registered" );

			// find machine IP
			var IP = _machineRegistry.GetMachineIP( machineId );

			//if ( !_machineRegistry.Machines.TryGetValue( machineId, out var m ) )
			//	throw new Exception($"Machine {machineId} not found for {path}@{machineId}");


			//foreach( var rec in m.Def.SshUrls )
			//{
			//	if( path.StartsWith( rec.Path, StringComparison.OrdinalIgnoreCase ) )
			//	{
			//		var pathRelativeToShare = path.Substring( rec.Path.Length ).Replace("\\", "/");

			//		var vars = new Dictionary<string,string>();

			//		var sshVars = SshStateProvider is null ? new Dictionary<string, string>() : SshStateProvider.GetVariables( machineId, "" );
			//		Tools.ExtendVars( vars, sshVars );

			//		vars["MACHINE_USERNAME"] = ""; // from machine
			//		vars["MACHINE_PASSWORD"] = "";
			//		vars["MACHINE_IP"] = IP;

			//		var urlPrefix = Tools.ExpandEnvAndInternalVars( rec.UrlPrefix , vars);
			//		return $"{urlPrefix}/{pathRelativeToShare}";
			//	}
			//}

			//throw new Exception($"Can't construct SSH path, no <SshPath/> record matching {path}@{machineId}");

			path = Path.GetFullPath( path ); // normalizes slashes
			var links = SshStateProvider.GetSymLinks( machineId );
			foreach (var link in links)
			{
				if (path.StartsWith( link.LocalPath, StringComparison.OrdinalIgnoreCase ))
				{
					var pathRelativeToShare = path.Substring( link.LocalPath.Length ).Replace( "\\", "/" );

					var vars = new Dictionary<string, string>();

					var sshVars = SshStateProvider is null ? new Dictionary<string, string>() : SshStateProvider.GetVariables( machineId, "" );
					Tools.ExtendVars( vars, sshVars );

					vars["MACHINE_USERNAME"] = ""; // from machine
					vars["MACHINE_PASSWORD"] = "";
					vars["MACHINE_IP"] = IP;

					var urlPrefix = $"sftp://%GW_USERNAME%:%GW_PASSWORD%@%GW_IP%:%GW_PORT%{link.GatewayDir}/{link.Name}";
					urlPrefix = Tools.ExpandEnvAndInternalVars( urlPrefix, vars );

					return $"{urlPrefix}/{pathRelativeToShare}";
				}
			}

			throw new Exception( $"Failed to construct SSH path for {path}@{machineId}, no matching file share" );
		}

		// "C:\myDir" => "/c:/myDir"
		public static string ConvertWindowsPathToSshPath( string path )
		{
			if(path.Length >= 3 && Char.IsLetter( path[0] ) && path[1] == ':' && (path[2] == '\\' || path[2] == '/') )
			{
				path = $"/{path[0].ToString().ToLower()}:/{path.Substring( 3 )}";
				return path.Replace( '\\', '/' );
			}
			return "";
		}
	}
}

