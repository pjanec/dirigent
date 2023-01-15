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
		

		string _scriptRootFolder;

		Dictionary<string, Func<Script>> _scriptCreators = new Dictionary<string, Func<Script>>(StringComparer.OrdinalIgnoreCase);

		public ScriptFactory( string scriptRootFolder )
		{
			_scriptRootFolder = scriptRootFolder;
			AddBuiltIns();
		}

		public void AddScript( string scriptName, Func<Script> scriptCreator )
		{
			_scriptCreators.Add( scriptName, scriptCreator );
		}

		void SetupScript( Script script, string title, byte[]? args, SynchronousIDirig ctrl, string? scriptOrigin, string? requestorId )
		{
			script.Dirig = ctrl;
			script.Title = title;
			script.Origin = string.IsNullOrEmpty(scriptOrigin) ? string.Empty : scriptOrigin;
			script.Args = args;
			script.Requestor = requestorId ?? "";
		}

		public T CreateFromLines<T>( string title, string[] codeLines, byte[]? args, SynchronousIDirig ctrl, string? scriptOrigin, string? requestorId ) where T: Script
		{
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

			var script = scriptIntf as T;
			if( script is null )
			{
				scriptIntf.Dispose();
				throw new Exception($"Script not derived from AsyncScript class!");
			}

			SetupScript( script, title, args, ctrl, scriptOrigin, requestorId );
			return script;
		}

		public T CreateFromFile<T>( string title, string fileName, byte[]? args, SynchronousIDirig ctrl, string? requestorId ) where T:Script
		{
			var lines = File.ReadAllLines( fileName );

			var script = CreateFromLines<T>( title, lines, args, ctrl, fileName, requestorId );

			return script;
		}

		public T CreateFromString<T>( Guid taskInstance, string id, string scriptCode, byte[]? args, SynchronousIDirig ctrl, string? scriptOrigin, string? requestorId ) where T:Script
		{
			var lines = Tools.ReadAllLinesFromString( scriptCode );

			var script = CreateFromLines<T>( id, lines, args, ctrl, scriptOrigin, requestorId );

			return script;
		}

		void AddBuiltIns()
		{
			AddScript( "BuiltIns/DemoScript1.cs", () => new DemoScript1() );
			AddScript( Scripts.BuiltIn.ResolveVfsPath._Name, () => new Scripts.BuiltIn.ResolveVfsPath() );
			AddScript( Scripts.BuiltIn.DownloadZipped._Name, () => new Scripts.BuiltIn.DownloadZipped() );
			AddScript( Scripts.BuiltIn.DownloadZippedSlave._Name, () => new Scripts.BuiltIn.DownloadZippedSlave() );
			AddScript( Scripts.BuiltIn.BrowseInDblCmdVirtPanel._Name, () => new Scripts.BuiltIn.BrowseInDblCmdVirtPanel() );
		}

		/// <summary>
		/// Instantiates the script. Runs from async context, should not use any stuff that is not thread safe.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="taskInstance"></param>
		/// <param name="title"></param>
		/// <param name="scriptName"></param>
		/// <param name="scriptRootFolder"></param>
		/// <param name="scriptCode"></param>
		/// <param name="args"></param>
		/// <param name="ctrl">WARNING must be a unique instance as each script modifies it</param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public T Create<T>( Guid taskInstance, string title, string scriptName, string? scriptRootFolder, string? scriptCode, byte[]? args, SynchronousIDirig ctrl, string? requestorId ) where T:Script
		{
			T? script = null;
			if( _scriptCreators.TryGetValue( scriptName, out var scriptCreator ) )
			{
				script = scriptCreator() as T;
			}
			
			if( script != null )
			{
				SetupScript( script, title, args, ctrl, scriptName, requestorId );
				return script;
			}
				
			if (!string.IsNullOrEmpty( scriptCode ))
			{
				script = CreateFromString<T>( taskInstance, title, scriptCode, args, ctrl, scriptName, requestorId );
			}
			else
			// code not provided
			// construct the file path from the script name and the script root folder
			if (!string.IsNullOrEmpty( scriptName ))
			{
				// script name can be an absolute path or relative to the script root folder
				// it also can be missing the extension (then .cs is used)

				string fileName = scriptName;

				fileName = Tools.ExpandEnvVars( fileName );

				if (!Path.IsPathRooted( fileName ))
				{
					if (!string.IsNullOrEmpty( scriptRootFolder ))
					{
						fileName = Path.Combine( scriptRootFolder, fileName );
					}
					else
					{
						throw new Exception( $"Failed to construct script file name, scriptRootFolder not specified." );
					}
				}

				if (!fileName.EndsWith( ".cs" ))
				{
					fileName = fileName + ".cs";
				}

				script = CreateFromFile<T>( title, fileName, args, ctrl, requestorId );
			}
			else
			{
				throw new Exception( $"Script name nor script code specified." );
			}

			return script;
		}
	}

}
