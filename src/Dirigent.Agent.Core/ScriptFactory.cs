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
	public class ScriptFactory
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		

		string? _scriptRootFolder;
				
		public ScriptFactory( string? scriptRootFolder )
		{
			_scriptRootFolder = scriptRootFolder;
		}

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
		
		public static Script CreateFromLines( string id, string[] codeLines, string? args, IDirig ctrl, string? scriptOrigin )
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
			script.Ctrl = ctrl;
			script.FileName = scriptOrigin ?? string.Empty;
			script.Args = args ?? string.Empty;

			return script;
		}

		public static Script CreateFromFile( string id, string fileName, string? args, IDirig ctrl )
		{
			var lines = File.ReadAllLines( fileName );

			var script = CreateFromLines( id, lines, args, ctrl, fileName );

			return script;
		}

		public static Script CreateFromString( string id, string scriptCode, string? args, IDirig ctrl, string? scriptOrigin )
		{
			var lines = Tools.ReadAllLinesFromString( scriptCode );

			var script = CreateFromLines( id, lines, args, ctrl, scriptOrigin );

			return script;
		}

		static Script? TryBuiltIns( string scriptName )
		{
			if( scriptName == "BuiltIns/DownloadFileZipped/Controller" )
				return new Scripts.DownloadFileZippedController();
			if( scriptName == "BuiltIns/DownloadFileZipped/Worker" )
				return new Scripts.DownloadFileZippedWorker();
			return null;				
		}
		
		public Script Create( string id, string scriptName, string? scriptCode, string? args, IDirig ctrl )
		{
			Script? script = TryBuiltIns( scriptName );

			if( script != null ) return script;
				
			if (!string.IsNullOrEmpty( scriptCode ))
			{
				script = CreateFromString( id, scriptCode, args, ctrl, scriptName );
			}
			else
			// code not provided
			// construct the file path from the script name and the script root folder
			if (!string.IsNullOrEmpty( scriptName ))
			{
				// script name can be an absolute path or relative to the script root folder
				// it also can be missing the extension (then .cs is used)

				string fileName = scriptName;

				if (!Path.IsPathRooted( fileName ))
				{
					if (!string.IsNullOrEmpty( _scriptRootFolder ))
					{
						fileName = Path.Combine( _scriptRootFolder, fileName );
					}
					else
					{
						throw new Exception( $"ailed to construct script file name, scriptRootFolder not specified." );
					}
				}

				if (!fileName.EndsWith( ".cs" ))
				{
					fileName = fileName + ".cs";
				}

				script = CreateFromFile( id, fileName, args, ctrl );
			}
			else
			{
				throw new Exception( $"Script name not script code specified." );
			}

			return script;
		}
	}

}
