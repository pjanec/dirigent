using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;
using System.IO;
using System.Reflection;

namespace Dirigent.Common
{
	public class Tools
	{
		public static ILaunchPlan FindPlanByName(IEnumerable<ILaunchPlan> planRepo, string planName)
		{
			// find plan in the repository
			ILaunchPlan plan;
			try
			{
				plan = planRepo.First((i) => i.Name == planName);
				return plan;
			}
			catch
			{
				throw new UnknownPlanName(planName);
			}

		}

		public static string GetAppStateString(AppIdTuple t, AppState appState)
		{
			var sbFlags = new StringBuilder();
			if (appState.Started) sbFlags.Append("S");
			if (appState.StartFailed) sbFlags.Append("F");
			if (appState.Running) sbFlags.Append("R");
			if (appState.Killed) sbFlags.Append("K");
			if (appState.Initialized) sbFlags.Append("I");
			if (appState.PlanApplied) sbFlags.Append("P");
			if (appState.Dying) sbFlags.Append("D");
			if (appState.Restarting) sbFlags.Append("X");

			var now = DateTime.UtcNow;

			var stateStr = String.Format(
				System.Globalization.CultureInfo.InvariantCulture,
				"APP:{0}:{1}:{2}:{3:0.00}:{4}:{5}:{6}:{7}",
				t.ToString(),
				sbFlags.ToString(),
				appState.ExitCode,
				(now - appState.LastChange).TotalSeconds,
				appState.CPU,
				appState.GPU,
				appState.Memory,
				appState.PlanName
			);

			return stateStr;
		}

		public static string GetPlanStateString(string planName, PlanState planState)
		{
			var stateStr = String.Format(
				System.Globalization.CultureInfo.InvariantCulture,
				"PLAN:{0}:{1}",
				planName,
				planState.OpStatus.ToString()
			);
			return stateStr;
		}

		// returs first line without CR/LF
		public static string JustFirstLine(string multiLineString)
		{
			var crPos = multiLineString.IndexOf('\r');
			var lfPos = multiLineString.IndexOf('\n');
			if (crPos >= 0 || lfPos >= 0)
			{
				return multiLineString.Substring(0, Math.Min(crPos, lfPos));
			}
			return multiLineString; // no other line found
		}

		public static string AssemblyDirectory
		{
			get
			{
				string codeBase = Assembly.GetExecutingAssembly().CodeBase;
				UriBuilder uri = new UriBuilder(codeBase);
				string path = Uri.UnescapeDataString(uri.Path);
				return Path.GetDirectoryName(path);
			}
		}

		/// <summary>
		/// Replaces %ENVVAR% in a string with actual value of evn vars; undefined ones will be replaced with empty string
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string ExpandEnvVars(String str, bool leaveUnknown=false)
		{

			System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(str, @"(%\w+%)");

			while( match.Success )
			{
				string varName = match.Value.Replace("%", "").Trim();
				string varValue = Environment.GetEnvironmentVariable(varName);
				
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
		public static string ExpandInternalVars(String str, Dictionary<string, string> variables, bool leaveUnknown=false)
		{

			System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(str, @"(%\w+%)");

			while( match.Success )
			{
				string varName = match.Value.Replace("%", "").Trim();
				string varValue;
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

		/// <summary>
		/// Replaces any %VARNAME% with an ampty string
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static string RemoveVars( String str )
		{

			System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(str, @"(%\w+%)");

			while( match.Success )
			{
				string varName = match.Value.Replace("%", "").Trim();
				string varValue = String.Empty;
				str = str.Replace( match.Value, varValue );
				match = match.NextMatch();
			}
			return str;
		}
}

}
