#if Windows
using System;
using System.Linq;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Dirigent;
using System.Diagnostics;

namespace Dirigent.Scripts.BuiltIn
{

	public class GetProcessInfo : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/GetProcessInfo.cs";

		public class TArgs
		{
			public int PID;
		};

		public class TResult
		{
			public string ExePath = "";
			public string CommandLine = "";
		}

		protected override Task<byte[]?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args );
			if( args is null ) throw new NullReferenceException("Args is null");
			if( args.PID == 0 ) throw new NullReferenceException("Args.PID is 0");

			var result = new TResult();
				
			string query =
				$@"SELECT ExecutablePath, CommandLine, Priority
					FROM Win32_Process
					WHERE ProcessId = {args.PID}";
 
			using (var searcher   = new ManagementObjectSearcher(query))
			using (var collection = searcher.Get())
			{
				foreach (var item in collection)
				{
					result.ExePath = (string) item["ExecutablePath"];
					result.CommandLine = (string) item["CommandLine"];
				}
			}

			// the following fails with invalid operation error
			// var process = Process.GetProcessById( args.PID );
			//result.Running = process.HasExited;
			//var envVars = process.StartInfo.EnvironmentVariables;
			//foreach( string key in envVars.Keys )
			//{
			//	result.EnvVars.Add( key.ToString(), envVars[key]!.ToString() );
			//}

			// all done!
			return Task.FromResult<byte[]?>( Tools.Serialize(result) );
		}

	}

}
#endif