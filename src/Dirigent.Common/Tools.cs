﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace Dirigent
{
	public class Tools
	{
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

		public static string GetClientStateText( ClientState st )
		{
			string stCode = "Not running";

			if( st.Connected )
			{
				stCode = "Connected";
			}

			var statusInfoAge = DateTime.UtcNow - st.LastChange;
			if( statusInfoAge > TimeSpan.FromSeconds( 3 ) )
			{
				stCode = string.Format( "Offline for {0:0} sec", statusInfoAge.TotalSeconds );
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

			var statusInfoAge = DateTime.UtcNow - st.LastChange;
			if( statusInfoAge > TimeSpan.FromSeconds( 3 ) )
			{
				stCode += string.Format( " (Offline for {0:0} sec)", statusInfoAge.TotalSeconds );
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
							   "APP:{0}:{1}:{2}:{3:0.00}:{4}:{5}:{6}:{7}",
							   t.ToString(),
							   flags,
							   appState.ExitCode,
							   ( now - appState.LastChange ).TotalSeconds,
							   appState.CPU,
							   appState.GPU,
							   appState.Memory,
							   appState.PlanName
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

		public static string GetScriptStateText( ScriptState st )
		{
			return st.StatusText;
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

		// Input:
		//   Script1::argument string
		// Output
		//   #1: Script1
		//	 #2: argument string
		//
		// Input:
		//   Script1
		// Output
		//   #1: Script1
		//	 #2: empty
		public static (string, string?) ParseScriptIdArgs( string input )
		{
			var parts = input.Split("::", 2);

			var id = parts.Length > 0 ? parts[0] : string.Empty;
			var args = parts.Length > 1 ? parts[1] : null;

			return (id, args);
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

	}

}
