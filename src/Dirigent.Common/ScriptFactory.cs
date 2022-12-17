﻿using System;
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
		

		public ScriptFactory()
		{
		}

		//private static string? GetScriptClassName( string[] scriptLines )
		//{
		//	// get class name as the first class derived from Script

		//	// catches something like:
		//	//    public class MyClass : Script
		//	var regex = new Regex( @"\s*(?:(?:public\s+)|(?:static\s+))*class\s+([a-zA-Z_0-9]+)\s*\:\s*([a-zA-Z_0-9]+)");

		//	foreach( var line in scriptLines )
		//	{
		//		Match match = regex.Match(line);
  //              if( match.Success )
  //              {
  //                  string className = match.Groups[1].Value;
		//			string baseClassName = match.Groups[2].Value;

		//			if( baseClassName == "Script" )
		//			{
		//				return className;
		//			}
		//		}
		//	}
		//	return null;

		//}
		
		void SetupScript( Script script, string title, byte[]? args, SynchronousIDirig ctrl, string? scriptOrigin )
		{
			script.Dirig = ctrl;
			script.Title = title;
			script.Origin = string.IsNullOrEmpty(scriptOrigin) ? string.Empty : scriptOrigin;
			script.Args = args;
		}

		public T CreateFromLines<T>( string title, string[] codeLines, byte[]? args, SynchronousIDirig ctrl, string? scriptOrigin ) where T: Script
		{
			//string? scriptClassName = GetScriptClassName( codeLines );
			//if( string.IsNullOrEmpty( scriptClassName ) )
			//{
			//	throw new Exception($"Script does not contain a class derived from Script (class MyClass : Script). {scriptOrigin}");
			//}

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

			SetupScript( script, title, args, ctrl, scriptOrigin );
			return script;
		}

		public T CreateFromFile<T>( string title, string fileName, byte[]? args, SynchronousIDirig ctrl ) where T:Script
		{
			var lines = File.ReadAllLines( fileName );

			var script = CreateFromLines<T>( title, lines, args, ctrl, fileName );

			return script;
		}

		public T CreateFromString<T>( Guid taskInstance, string id, string scriptCode, byte[]? args, SynchronousIDirig ctrl, string? scriptOrigin ) where T:Script
		{
			var lines = Tools.ReadAllLinesFromString( scriptCode );

			var script = CreateFromLines<T>( id, lines, args, ctrl, scriptOrigin );

			return script;
		}

		Script? TryBuiltIns( string scriptName )
		{
			if( scriptName == "Scripts/DemoScript1.cs" )
				return new DemoScript1();
			if( scriptName == Scripts.BuiltIn.ResolveVfsPath._Name )
				return new Scripts.BuiltIn.ResolveVfsPath();
			//if( scriptName == "BuiltIns/DownloadFileZipped/Controller" )
			//	return new Scripts.DownloadFileZipped.Controller();
			//if( scriptName == "BuiltIns/DownloadFileZipped/Worker" )
			//	return new Scripts.DownloadFileZipped.Worker();
			//if( scriptName == Scripts.ResolveVfsTree.Controller._Name )
			//	return new Scripts.ResolveVfsTree.Controller();

			return null;				
		}
		
		/// <summary>
		/// 
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
		public T Create<T>( Guid taskInstance, string title, string scriptName, string? scriptRootFolder, string? scriptCode, byte[]? args, SynchronousIDirig ctrl ) where T:Script
		{
			T? script = TryBuiltIns( scriptName ) as T;
			if( script != null )
			{
				SetupScript( script, title, args, ctrl, scriptName );
				return script;
			}
				
			if (!string.IsNullOrEmpty( scriptCode ))
			{
				script = CreateFromString<T>( taskInstance, title, scriptCode, args, ctrl, scriptName );
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

				script = CreateFromFile<T>( title, fileName, args, ctrl );
			}
			else
			{
				throw new Exception( $"Script name nor script code specified." );
			}

			return script;
		}
	}

}