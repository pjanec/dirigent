using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dirigent
{
	public enum EPathType
	{
		Unknown,
		Local,  // C:\path
		UNC,    // \\server\share\path or 
		SSH,    // sftp://user@host:port/path or scp://user@host:port/path
	}

	public static class PathTools
	{
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

		public static bool IsPathLocalOrUNC( string? path )
		{
			var type = GetPathType( path );
			return type == EPathType.Local || type == EPathType.UNC;
		}
		
		public static bool IsPathSsh( string? path )
		{
			return GetPathType(path) == EPathType.SSH;
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

		// parse sftp://user:password@host:port/path or scp://user@host:port/path
		public class ParsedSshPath
		{
			public string Protocol = "";
			public string Host = "";
			public int Port = 0;
			public string User = "";
			public string Password = "";
			public string Path = ""; // "/C:/myDir"

			public ParsedSshPath() {}

			public ParsedSshPath( string sshPath )
			{
				if (!TryParseSshPath( sshPath, out var pp ))
					throw new ArgumentException( "Not an SSH path: " + sshPath );
				InitFrom( pp );
			}

			void InitFrom( ParsedSshPath pp )
			{
				Protocol = pp.Protocol;
				Host = pp.Host;
				Port = pp.Port;
				User = pp.User;
				Password = pp.Password;
				Path = pp.Path;
			}
		}
		
		public static bool TryParseSshPath( string sshPath, out ParsedSshPath pp )
		{
			pp = new ParsedSshPath();
			if (!IsPathSsh( sshPath ))
				return false;
				
			var r = new Regex( @"^(?<protocol>[^:]+)://(?<user>[^:@]+)(:(?<password>[^@]+))?@(?<host>[^:/]+)(:(?<port>\d+))?(?<path>/.*)$" );
			var m = r.Match( sshPath );
			if (m.Success)
			{
				pp.Protocol = m.Groups["protocol"].Value;
				pp.Host = m.Groups["host"].Value;
				pp.Port = string.IsNullOrEmpty(m.Groups["port"].Value) ? 22 : int.Parse( m.Groups["port"].Value );
				pp.User = m.Groups["user"].Value;
				pp.Password = m.Groups["password"].Value;
				pp.Path = m.Groups["path"].Value;
				return true;
			}

			return false;
		}
	}
}
