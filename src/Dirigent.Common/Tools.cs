using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace Dirigent
{
	public class Tools
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		static List<string>? _localIPv4Addresses;

		/// <summary>
		/// The IPv4 addresses of this machine, as its own network interfaces report them.
		/// </summary>
		/// <remarks>
		/// The only address Dirigent knew about a machine was what the config declared - usually
		/// nothing - or where its connection came from, which is loopback for anything running beside
		/// the master and a NAT address from behind a router. Neither identifies the machine.
		///
		/// Everything routable is returned and nothing is chosen: a machine legitimately has several
		/// addresses (the simulation network, the office LAN, a VPN, virtual switches), and guessing
		/// which one matters by adapter name is the kind of heuristic that fails on the one machine
		/// somebody is trying to diagnose. Loopback and link-local (169.254) addresses identify
		/// nothing and are left out.
		///
		/// Read once and remembered: it is asked for on every connection, and an address change is
		/// picked up the next time the process starts or reconnects.
		/// </remarks>
		public static List<string> LocalIPv4Addresses
		{
			get
			{
				if( _localIPv4Addresses is not null ) return _localIPv4Addresses;

				var found = new List<string>();

				try
				{
					foreach( var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces() )
					{
						if( nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up ) continue;
						if( nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ) continue;

						foreach( var addr in nic.GetIPProperties().UnicastAddresses )
						{
							if( addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ) continue;
							if( System.Net.IPAddress.IsLoopback( addr.Address ) ) continue;

							var text = addr.Address.ToString();
							if( text.StartsWith( "169.254." ) ) continue; // link-local, i.e. no address at all

							if( !found.Contains( text ) ) found.Add( text );
						}
					}
				}
				catch( Exception e )
				{
					log.Warn( $"Could not read the local network addresses: {e.Message}" );
				}

				_localIPv4Addresses = found;
				return _localIPv4Addresses;
			}
		}

		/// <summary>
		/// Makes a string usable as a file name: the characters Windows refuses become underscores.
		/// </summary>
		/// <remarks>
		/// Node titles reach file names - a download names its archive after the node it collected -
		/// and a title is free text that nobody writes with file naming in mind. A colon in one used
		/// to fail the download at the very end, after all the collecting was already done.
		/// </remarks>
		public static string SanitizeFileName( string name )
		{
			var invalid = System.IO.Path.GetInvalidFileNameChars();
			var sb = new System.Text.StringBuilder();
			foreach( var c in name )
			{
				sb.Append( Array.IndexOf( invalid, c ) >= 0 ? '_' : c );
			}

			// a trailing dot or space is legal to write and impossible to open afterwards
			var res = sb.ToString().Trim( ' ', '.' );
			return string.IsNullOrEmpty( res ) ? "_" : res;
		}

		public static bool BoolFromString( string boolString )
		{
			return ( new List<string>() { "1", "YES", "Y", "TRUE" } .Contains( boolString.ToUpper() ) );
		}

		public static PlanDef FindPlanByName( IEnumerable<PlanDef> planRepo, string planName )
		{
			// find plan in the repository
			PlanDef plan;
			try
			{
				plan = planRepo.First( ( i ) => i.Name == planName );
				return plan;
			}
			catch
			{
				throw new UnknownPlanName( planName );
			}

		}

		public static string GetClientStateText( ClientState? st )
		{
			string stCode = "Offline";

			if( st != null )
			{
				if( st.Connected )
				{
					stCode = "Online";
				}

				var statusInfoAge = DateTime.UtcNow - st.LastChange;
				if( statusInfoAge > TimeSpan.FromSeconds( 3 ) )
				{
					stCode = string.Format( "Offline for {0:0} sec", statusInfoAge.TotalSeconds );
				}
			}
			return stCode;
		}

		public static string GetAppStateText( AppState st, PlanState? planState, AppDef? appDef )
		{
			string stCode = "Not running";

			bool isPartOfPlan = !string.IsNullOrEmpty(st.PlanName) && (appDef is not null);

			if (planState != null)
			{
				var planRunning = planState.Running;
				if (planState.Running && !st.PlanApplied && isPartOfPlan && (!appDef?.Disabled ?? true))
				{
					stCode = "Planned";
				}
			}

			if ( st.Started )
			{
				if( st.Running )
				{
					if( st.Dying )
					{
						stCode = "Dying";
					}
					else if( !st.Initialized )
					{
						stCode = "Initializing";
					}
					else
					{
						stCode = "Running";
					}
				}
				else
					// !st.Running
				{
					if( st.Restarting )
					{
						stCode = "Restarting";
						if( st.RestartsRemaining >= 0 ) stCode += String.Format( " ({0} remaining)", st.RestartsRemaining );
					}
					else if( st.Killed )
					{
						stCode = "Killed";
					}
					else
					{
						stCode = string.Format( "Terminated ({0})", st.ExitCode );
					}
				}
			}
			else if( st.StartFailed )
			{
				stCode = "Failed to start";
			}

			if( st.LastChange.Ticks == 0 )
			{
				stCode += string.Format( " (Offline)" );
			}
			else
			{
				var statusInfoAge = DateTime.UtcNow - st.LastChange;
				if( statusInfoAge > TimeSpan.FromSeconds( 3 ) )
				{
					stCode += string.Format( " (Offline for {0:0} sec)", statusInfoAge.TotalSeconds );
				}
			}

			return stCode;
		}

		public static string GetAppStateFlags( AppState? appState )
		{
			if( appState is null )
				return string.Empty;

			var sbFlags = new StringBuilder();
			if( appState.Started ) sbFlags.Append( "S" );
			if( appState.StartFailed ) sbFlags.Append( "F" );
			if( appState.Running ) sbFlags.Append( "R" );
			if( appState.Killed ) sbFlags.Append( "K" );
			if( appState.Initialized ) sbFlags.Append( "I" );
			if( appState.PlanApplied ) sbFlags.Append( "P" );
			if( appState.Dying ) sbFlags.Append( "D" );
			if( appState.Restarting ) sbFlags.Append( "X" );

			return sbFlags.ToString();
		}

		public static string GetAppStateString( AppIdTuple t, AppState? appState )
		{
			if( appState is null )
				return string.Empty;

			var flags = GetAppStateFlags( appState );

			var now = DateTime.UtcNow;

			var stateStr = String.Format(
							   System.Globalization.CultureInfo.InvariantCulture,
							   "APP:{0}:{1}:{2}:{3:0.00}:{4}:{5}:{6}:{7}:{8}",
							   t.ToString(),
							   flags,
							   appState.ExitCode,
							   ( now - appState.LastChange ).TotalSeconds,
							   appState.CPU,
							   appState.GPU,
							   appState.Memory,
							   appState.PlanName,
							   appState.PID
						   );

			return stateStr;
		}

		public static string GetPlanStateFlags( PlanState planState )
		{
			if( planState is null )
				return string.Empty;

			var sbFlags = new StringBuilder();
			if( planState.Running ) sbFlags.Append( "R" );
			if( planState.Killing ) sbFlags.Append( "K" );

			return sbFlags.ToString();
		}

		public static string GetPlanStateText( PlanState st )
		{
			return st.OpStatus.ToString();
		}

		public static string GetPlanStateString( string planName, PlanState? planState )
		{
			if( planState is null )
				return string.Empty;

			var stateStr = String.Format(
							   System.Globalization.CultureInfo.InvariantCulture,
							   "PLAN:{0}:{1}",
							   planName,
							   planState.OpStatus.ToString()
						   );
			return stateStr;
		}

		public static string GetClientStateString( string machineId, ClientState? state )
		{
			if( state is null )
				return string.Empty;

			double age = ( DateTime.UtcNow - state.LastChange ).TotalSeconds;
			var stateStr = String.Format(
							   System.Globalization.CultureInfo.InvariantCulture,
							   "CLIENT:{0}:{1}:{2:0.0}:{3}",
							   state.Ident?.Name,
							   state.Connected?"1":"0",
							   age,
							   state.IP
						   );
			return stateStr;
		}

		public static string GetScriptStateText( ScriptState st )
		{
			return st.Text ?? "";
		}

		public static string GetScriptStateString( Guid scriptId, ScriptState? scriptState )
		{
			if( scriptState is null )
				return string.Empty;

			var stateJsonStr = Tools.Serialize( scriptState );
			return $"SCRIPT:{scriptId}:{stateJsonStr}";
		}

		/// <summary>
		/// What an object says about itself, or a note about why it could not - never an exception.
		/// </summary>
		/// <remarks>
		/// For the log lines on the message dispatch path. A message is logged just before it is
		/// sent, so a ToString() that throws does not spoil a line of the log - it stops the send and
		/// fails the operation the message was carrying. That has happened; see MessageToStringTests.
		/// The individual defect is worth fixing where it is, and this makes the next one cost a
		/// puzzling log line instead of an outage.
		/// </remarks>
		public static string SafeToString( object? o )
		{
			if( o is null ) return "(null)";

			try
			{
				return o.ToString() ?? "(null)";
			}
			catch( Exception e )
			{
				return $"({o.GetType().Name}.ToString() failed: {JustFirstLine( e.Message )})";
			}
		}

		// returs first line without CR/LF
		public static string JustFirstLine( string multiLineString )
		{
			var crPos = multiLineString.IndexOf( '\r' );
			var lfPos = multiLineString.IndexOf( '\n' );
			if( crPos >= 0 || lfPos >= 0 )
			{
				return multiLineString.Substring( 0, Math.Min( crPos, lfPos ) );
			}
			return multiLineString; // no other line found
		}

		//public static string AssemblyDirectory
		//{
		//	get
		//	{
		//		string codeBase = Assembly.GetExecutingAssembly().Location;
		//		UriBuilder uri = new UriBuilder(codeBase);
		//		string path = Uri.UnescapeDataString(uri.Path);
		//		return Path.GetDirectoryName(path);
		//	}
		//}

		/// <summary>
		/// Replaces %ENVVAR% in a string with actual value of evn vars; undefined ones will be replaced with empty string
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string ExpandEnvVars( String str, bool leaveUnknown = false )
		{

			System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match( str, @"(%\w+%)" );

			while( match.Success )
			{
				string varName = match.Value.Replace( "%", "" ).Trim();
				string? varValue = Environment.GetEnvironmentVariable( varName );

				bool replace = true;

				if( varValue == null )
				{
					if( leaveUnknown )	// do not replace, leave as is
					{
						replace = false;
					}
					else // replace the unknown var with empty string
					{
						varValue = String.Empty;
					}
				}

				if( replace )
				{
					str = str.Replace( match.Value, varValue );
				}
				match = match.NextMatch();
			}
			return str;
		}

		public static void AddOrUpdateVar( Dictionary<string, string> vars, string name, string? value )
		{
			// add extra var
			if( !string.IsNullOrEmpty(value) )
			{
				vars[name] = value;
			}
			else // uset the var if empty value if provided
			{
				vars.Remove(name);
			}
		}

		/// <summary>
		/// Replaces %VARNAME% in a string with actual value of the variable from given disctionary; undefined ones will be replaced with empty string
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string ExpandInternalVars( String str, Dictionary<string, string> variables, bool leaveUnknown = false )
		{

			System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match( str, @"(%\w+%)" );

			while( match.Success )
			{
				string varName = match.Value.Replace( "%", "" ).Trim();
				string? varValue;
				bool replace = true;
				if( !variables.TryGetValue( varName, out varValue ) )
				{
					if( leaveUnknown )	// do not replace, leave as is
					{
						replace = false;
					}
					else // replace the unknown var with empty string
					{
						varValue = String.Empty;
					}
				}

				if( replace )
				{
					str = str.Replace( match.Value, varValue );
				}

				match = match.NextMatch();
			}
			return str;
		}

		///// <summary>
		///// Replaces %1  %2 etc. in a string with actual value from given array
		///// </summary>
		//public static string ExpandNumericVars(String str, List<string> parameters)
		//{
		//
		//	System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(str, @"(%\d+)");
		//
		//	while( match.Success )
		//	{
		//		string varName = match.Value.Replace("%", "").Trim();
		//		int varIndex = -1;
		//		try{
		//		  varIndex = Convert.ToInt32(varName);
		//		}
		//		catch
		//		{
		//		}
		//
		//		string varValue = String.Empty;
		//		if( varIndex >=0 && varIndex < parameters.Count )
		//		{
		//			varValue = parameters[varIndex];
		//		}
		//
		//		str = str.Replace( match.Value, varValue );
		//		match = match.NextMatch();
		//	}
		//	return str;
		//}

		public static string ExpandEnvAndInternalVars( string str, Dictionary<string, string>? internalVars=null )
		{
			if( string.IsNullOrEmpty( str ) ) return string.Empty;

			var s = Tools.ExpandEnvVars( str, true );
			//s = ExpandNumericVars( s, numericParams, true );
			if( internalVars is not null )
			{
				s = Tools.ExpandInternalVars( s, internalVars, true );
			}
			s = Tools.RemoveVars( s ); // replace the remaining vars with en empty string
			return s;
		}

		/// <summary>
		/// Replaces any %VARNAME% with an ampty string
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string RemoveVars( String str )
		{

			System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match( str, @"(%\w+%)" );

			while( match.Success )
			{
				string varName = match.Value.Replace( "%", "" ).Trim();
				string varValue = String.Empty;
				str = str.Replace( match.Value, varValue );
				match = match.NextMatch();
			}
			return str;
		}

		// parses a list of strings in format key=value into a dictionary
		public static Dictionary<string, string> ParseKeyValList( IList<string> args )
		{
			var res = new Dictionary<string, string>();
			foreach( var a in args )
			{
				var arr = a.Split( new char[] {'='}, 2 );
				if( arr.Length == 2 )
				{
					res.Add( arr[0].Trim(), arr[1].Trim() );
				}
			}
			return res;
		}

		public static bool TryGetValueIgnoreKeyCase( Dictionary<string, string> keyValArgs, string key, out string value )
		{
			foreach( var kv in keyValArgs )
			{
				if( string.Compare( kv.Key, key, true ) == 0 )
				{
					value = kv.Value;
					return true;
				}
			}
			value = string.Empty;
			return false;
		}

		public static bool GetEnumValueByNameIgnoreCase<T>( string name, out T? value ) where T: IComparable
		{
			int i = 0;
			foreach( var eName in Enum.GetNames( typeof( T ) ) )
			{
				if( string.Compare( eName, name, true ) == 0 )
				{
					// strange way how to get enum value :-(
					var enumValues = Enum.GetValues( typeof( T ) ).Cast<T>();
					int j = 0;
					T? y = default( T );
					foreach( T x in enumValues )
					{
						y = x;
						if( j == i ) break;
					}
					value = y;
					return true;
				}
				i++;
			}
			value = default( T );
			return false;
		}

		public static string? GetExePath()
		{
			var assemblyExe = Assembly.GetEntryAssembly()?.Location;
			if( assemblyExe == null ) return null;
			if( assemblyExe.StartsWith( "file:///" ) ) assemblyExe = assemblyExe.Remove( 0, 8 );
			return assemblyExe;
		}

		public static string GetExeDir()
		{
			return System.IO.Path.GetDirectoryName( GetExePath() ) ?? string.Empty;
		}

        /// <summary>
        /// Replaces the existing optin value in given command line with a new value, or adds the option at the end if not existing yet
        /// </summary>
        public static string[] AddOrReplaceCmdLineOptionWithValue( ReadOnlySpan<string> args, string optionText, string newValue )
        {
            var res = new List<string>();
            string? prevArg = null;
            bool optionFound = false;
            foreach( var x in args )
            {
                string newArg = x;
                if( prevArg == optionText )
                {
                    newArg = newValue;
                    optionFound = true;
                }
                res.Add( newArg );
				prevArg = newArg;
            }
            if(!optionFound)
            {
                res.Add( optionText );
                res.Add( newValue );
            }
            return res.ToArray();
        }

		/// <summary>
		/// m1.a1, m1.a1@plan1
		/// on error returns empty strings
		/// </summary>
		public static (AppIdTuple, string?) ParseAppIdWithPlan( string input )
		{
			int amperIndex = input.IndexOf('@');
			if( amperIndex >= 0 )
			{
				var appIdTuple = new AppIdTuple( input.Substring(0, amperIndex) );
				var planName = input.Substring(amperIndex+1).Trim();
				return (appIdTuple, planName);
			}
			else
			{
				var appIdTuple = new AppIdTuple( input );
				return (appIdTuple, null);
			}
		}

		/// <summary>
		/// Parses the stringized value list into a dictionary.
		/// Format of string: VAR1=VALUE1::VAR2=VALUE2
		/// Throws on error!
		/// </summary>
		/// <param name="vars">if null, fucvtion returns null</param>
		/// <returns>null if vars is null, otherwise valid dict object</returns>
		public static Dictionary<string,string>? ParseEnvVarList( string? vars )
		{
			if( vars is null ) return null;

			// split & parse
			var varList = new Dictionary<string,string>();
			foreach( var kv in vars.Split(new string[] { "::" }, StringSplitOptions.None))
			{
				if( string.IsNullOrWhiteSpace(kv) ) // nothing present
				{
					//throw new Exception($"Invalid SetVars format: {kv}");
					continue;
				}

				int equalSignIdx = kv.IndexOf("=");

				if( equalSignIdx < 0 ) // equal sign not present
				{
					throw new Exception($"Invalid SetVars format: {kv}");
				}

				string name = kv.Substring(0, equalSignIdx).Trim();
				string value = kv.Substring(equalSignIdx+1).TrimStart();
				
				if( string.IsNullOrEmpty(name) )
				{
					throw new Exception($"Invalid SetVars format: {kv}");
				}

				varList[name] = value;
			}
			return varList;
		}

		public static string EnvVarListToString( Dictionary<string,string>? varList )
		{
			if( varList is null ) return "<null>";
			return "[" + string.Join( "::", from x in varList select $"{x.Key}={x.Value}" ) + "]";
		}

		public static bool IsAppInPlan( IDirig iDirig, AppIdTuple appId, PlanDef? planDef )
		{
			if( planDef is null ) return false; // the plan does not exists
			var appDef = planDef.AppDefs.Find( x => x.Id == appId );
			return appDef is not null; // app is part of the plan
		}

		public static bool IsAppInPlan( IDirig iDirig, AppIdTuple appId, string? planId )
		{
			if( planId is null ) return false;
			var planDef = iDirig.GetPlanDef( planId );
			return IsAppInPlan( iDirig, appId, planDef );
		}

		public static bool GetRemoteIpAndPort( System.Net.Sockets.Socket s, out string ipAddress, out int port  )
		{
			var remoteIpEndPoint = s.RemoteEndPoint as System.Net.IPEndPoint;

			if (remoteIpEndPoint != null)
			{
				ipAddress = remoteIpEndPoint.Address.ToString();
				port = remoteIpEndPoint.Port;
				return true;
			}
			ipAddress = String.Empty;
			port = 0;
			return false;
		}

		public static IEnumerable<string> ReadLines( Stream stream, Encoding encoding)
		{
			using (var reader = new StreamReader(stream, encoding))
			{
				string? line;
				while ((line = reader.ReadLine()) != null)
				{
					yield return line;
				}
			}
		}
			
		public static Stream GenerateStreamFromString(string s)
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.Write(s);
			writer.Flush();
			stream.Position = 0;
			return stream;
		}

		public static string[] ReadAllLinesFromString( string fileContent )
		{
			using( var stream = GenerateStreamFromString( fileContent ) )
			{
				return ReadLines( stream, Encoding.UTF8 ).ToArray();
			}
		}

		public static JsonSerializerSettings JsonSerTypeNameHandlingAuto = new JsonSerializerSettings
		{
			TypeNameHandling = TypeNameHandling.Auto,
			Converters = new List<JsonConverter> { new Newtonsoft.Json.Converters.StringEnumConverter() },
			NullValueHandling = NullValueHandling.Ignore
		};

		public static string Serialize<T>( T data )	
		{
			return JsonConvert.SerializeObject( data, JsonSerTypeNameHandlingAuto );
		}

		public static T? Deserialize<T>( string? data )	
		{
			if( data == null ) return default(T);
			return JsonConvert.DeserializeObject<T>( data, JsonSerTypeNameHandlingAuto );
		}

		/// <summary> serialze/deserialize-based cloning </summary>
		public static T? Clone<T>( T? data )	
		{
			if (data is null) return default(T);
			return Deserialize<T>( Serialize( data ) );
		}

		public static string HumanReadableSize( ulong bytes )
		{
			string[] sizes = { "B", "KB", "MB", "GB", "TB" };
			double len = bytes;
			int order = 0;
			while (len >= 1024 && order < sizes.Length - 1)
			{
				order++;
				len = len / 1024;
			}
			return String.Format(
				CultureInfo.InvariantCulture,
				"{0} {1}",
				RoundToSignificantDigits(len,3),
				sizes[order]
			);
		}

		public static string HumanReadableSizeOutOf( ulong bytes, ulong total )
		{
			string[] sizes = { "B", "KB", "MB", "GB", "TB" };
			double len1 = bytes;
			double len2 = total;
			int order = 0;
			while (len2 >= 1024 && order < sizes.Length - 1)
			{
				order++;
				len1 = len1 / 1024;
				len2 = len2 / 1024;
			}
			return String.Format(
				CultureInfo.InvariantCulture,
				"{0}/{1} {2}",
				RoundToSignificantDigits(len1, 3),
				RoundToSignificantDigits(len2, 3),
				sizes[order]
			);
		}

		public static string GetHomePath()
		{
			// Not in .NET 2.0
			// System.Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (System.Environment.OSVersion.Platform == System.PlatformID.Unix)
				return System.Environment.GetEnvironmentVariable("HOME")!;

			if( System.Environment.OSVersion.Platform == System.PlatformID.Win32NT )
				return System.Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

			return String.Empty;
		}
		
		public static string GetDownloadFolderPath()
		{
			#if Windows
				return System.Convert.ToString(
					Microsoft.Win32.Registry.GetValue(
						 @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"
						,"{374DE290-123F-4565-9164-39C4925E467B}"
						,String.Empty
					)
				)!;
			#else
				string pathDownload = System.IO.Path.Combine(GetHomePath(), "Downloads");
				return pathDownload;
			#endif			
		}

		public static void SetDefaultEnvVars( string? sharedConfigDir )
		{
			Environment.SetEnvironmentVariable( "DIRIGENT_BIN", Tools.GetExeDir() );
			
			if( sharedConfigDir != null )
				Environment.SetEnvironmentVariable( "DIRIGENT_SHAREDCONFDIR", sharedConfigDir );
		}
		
		public static string RoundToSignificantDigits( double d, int digits )
		{
			if(d == 0)
				return "0";

			double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1);
			double rounded = scale * Math.Round(d / scale, digits);

			string s = rounded.ToString( CultureInfo.InvariantCulture );
			int digitsLeft = 3;
			int pos = 0;
			while (pos < s.Length && digitsLeft > 0)
			{
				if( s[pos] != '.')
					digitsLeft--;
				pos++;
			}
			return s.Substring( 0, pos );

		}

		public static void SendWakeOnLanMagicPacket(string macAddress)
		{
			UdpClient udpClient = new UdpClient();
    
			// enable UDP broadcasting for UdpClient
			udpClient.EnableBroadcast = true;

			var dgram = new byte[1024];

			// 6 magic bytes
			for (int i = 0; i < 6; i++)
			{
				dgram[i] = 255;
			}

			// convert MAC-address to bytes
			byte[] address_bytes = new byte[6];
			for (int i = 0; i < 6; i++)
			{
				address_bytes[i] = byte.Parse(macAddress.Substring(3 * i, 2), NumberStyles.HexNumber);
			}

			// repeat MAC-address 16 times in the datagram
			var macaddress_block = dgram.AsSpan(6, 16 * 6);
			for (int i = 0; i < 16; i++)
			{
				address_bytes.CopyTo(macaddress_block.Slice(6 * i));
			}

			// send datagram using UDP and port 0
			udpClient.Send(dgram, dgram.Length, new System.Net.IPEndPoint( System.Net.IPAddress.Broadcast, 7));
			udpClient.Close();
		}
}

}
