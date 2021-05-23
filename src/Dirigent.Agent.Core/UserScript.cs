using System;
using System.Text.RegularExpressions;

namespace Dirigent
{
	/// <summary>
	/// Interface used for dynamic instantiation of scripts
	/// </summary>
	interface IUserScript : ITickable
	{
		//string StatusText { get; set; }
		//ScriptCtrl Ctrl { get; set; }
		//string Args { get; set; }
		//void Init();
		//void Done();
	}

	/// <summary>
	/// Script instantiated dynamically from a C# source file
	/// </summary>
	public class UserScript : Disposable, IUserScript
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		
		public string Id { get; set; } = string.Empty;

		public bool ShallBeRemoved { get; protected set; }

		public uint Flags => 0;

		public string StatusText { get; set; } = string.Empty;

		public string FileName { get; set; } = string.Empty;

		public string Args { get; set; } = string.Empty;

		public Action? OnRemoved { get; set; }

		// initialized during installation
		#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public ScriptCtrl Ctrl { get; set; }
		#pragma warning restore CS8618


		protected Coroutine? Coroutine;

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;

			Done();
			
			if( Coroutine != null )
			{
				Coroutine.Dispose();
				Coroutine = null;
			}
		}

		/// <summary> called once when script gets instantiated </summary>
		public virtual void Init()
		{
		}

		/// <summary> called once when script gets destroyed </summary>
		public virtual void Done()
		{
		}

		/// <summary> called every frame </summary>
		public virtual void Tick()
		{
		}

		void ITickable.Tick()
		{
			// tick coroutine if exists; remove script when coroutine finishes
			if( Coroutine != null )
			{
				Coroutine.Tick();
				if( Coroutine.IsFinished )
				{
					Coroutine.Dispose();
					Coroutine = null;
					
					ShallBeRemoved = true;
				}
			}

			// call the virtual method
			Tick();
		}

		private static void InitScriptInstance( UserScript script, string id, Master master, string? fileName, string? args )
		{
			script.Id = id;
			script.Ctrl = new ScriptCtrl(master);
			script.FileName = fileName ?? string.Empty;
			script.Args = args ?? string.Empty;

			script.Init();
		}

		private static string? GetScriptClassName( string scriptFileName )
		{
			// get class name as the first class derived from Script
			var fileLines = System.IO.File.ReadAllLines( scriptFileName );

			// catches something like:
			//    public class MyClass : Script
			var regex = new Regex( @"\s*(?:(?:public\s+)|(?:static\s+))*class\s+([a-zA-Z_0-9]+)\s*\:\s*([a-zA-Z_0-9]+)");

			foreach( var line in fileLines )
			{
				Match match = regex.Match(line);
                if( match.Success )
                {
                    string className = match.Groups[1].Value;
					string baseClassName = match.Groups[2].Value;

					if( baseClassName == "UserScript" )
					{
						return className;
					}
				}
			}
			return null;

		}

		public static UserScript CreateFromFile( string id, string fileName, string? args, Master master )
		{
			log.Debug( $"Loading script file '{fileName}'" );

			string? scriptClassName = GetScriptClassName( fileName );
			if( string.IsNullOrEmpty( scriptClassName ) )
			{
				throw new Exception($"Script does not contain a class derived from UserScript (class MyClass : UserScript). File: {fileName}");
			}

			IUserScript scriptIntf = CSScriptLib.CSScript.Evaluator
								.ReferenceAssemblyByName("System")
								.ReferenceAssemblyByName("log4net")
								.ReferenceAssemblyByName("Dirigent.Common")
								.ReferenceAssemblyByName("Dirigent.Agent.Core")
								.LoadFile<IUserScript>(fileName)
								;
			if( scriptIntf == null )
			{
				throw new Exception($"Not a valid script file: {fileName}");
			}

			var script = scriptIntf as UserScript;
			if( script is null )
			{
				scriptIntf.Dispose();
				throw new Exception($"Script not derived from Script class! File: {fileName}");
			}

			Dirigent.UserScript.InitScriptInstance( script, id, master, fileName, args );

			return script;
		}
	}

}
