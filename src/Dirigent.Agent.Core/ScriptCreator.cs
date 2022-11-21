using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dirigent
{
	/// <summary>
	/// Script instantiated dynamically from a C# source file
	/// </summary>
	public static class ScriptCreator
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		

		private static string? GetScriptClassName( string[] scriptLines )
		{
			// get class name as the first class derived from Script

			// catches something like:
			//    public class MyClass : Script
			var regex = new Regex( @"\s*(?:(?:public\s+)|(?:static\s+))*class\s+([a-zA-Z_0-9]+)\s*\:\s*([a-zA-Z_0-9]+)");

			foreach( var line in scriptLines )
			{
				Match match = regex.Match(line);
                if( match.Success )
                {
                    string className = match.Groups[1].Value;
					string baseClassName = match.Groups[2].Value;

					if( baseClassName == "Script" )
					{
						return className;
					}
				}
			}
			return null;

		}
		
		public static Script CreateFromLines( string id, string[] codeLines, string? args, Master master, string? scriptOrigin )
		{
			string? scriptClassName = GetScriptClassName( codeLines );
			if( string.IsNullOrEmpty( scriptClassName ) )
			{
				throw new Exception($"Script does not contain a class derived from Script (class MyClass : Script). {scriptOrigin}");
			}

			IScript scriptIntf = CSScriptLib.CSScript.Evaluator
								.ReferenceAssemblyByName("System")
								.ReferenceAssemblyByName("log4net")
								.ReferenceAssemblyByName("Dirigent.Common")
								.ReferenceAssemblyByName("Dirigent.Agent.Core")
								.LoadCode<IScript>( string.Join("\r\n", codeLines) )
								;
			if( scriptIntf == null )
			{
				throw new Exception($"Not a valid script");
			}

			var script = scriptIntf as Script;
			if( script is null )
			{
				scriptIntf.Dispose();
				throw new Exception($"Script not derived from Script class!");
			}

			script.Id = id;
			script.Ctrl = master;
			script.FileName = scriptOrigin ?? string.Empty;
			script.Args = args ?? string.Empty;

			return script;
		}

		public static Script CreateFromFile( string id, string fileName, string? args, Master master )
		{
			var lines = File.ReadAllLines( fileName );

			var script = CreateFromLines( id, lines, args, master, fileName );

			return script;
		}

		public static Script CreateFromString( string id, string scriptContent, string? args, Master master, string? scriptOrigin )
		{
			var lines = Tools.ReadAllLinesFromString( scriptContent );

			var script = CreateFromLines( id, lines, args, master, scriptOrigin );

			return script;
		}
	}

}
