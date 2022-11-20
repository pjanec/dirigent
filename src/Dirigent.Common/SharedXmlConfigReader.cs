using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using X = Dirigent.XmlConfigReaderUtils;
using System.Diagnostics;

namespace Dirigent
{

	public class SharedXmlConfigReader
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public SharedConfig cfg;
		XDocument doc;
		XElement root; // the top level XML element

		/// <summary>
		/// All FileDefs found across the SharedConfig file
		/// </summary>
		List<FileDef> _files = new List<FileDef>();
		
		/// <summary>
		/// All FilePackageDefs found across the SharedConfig file
		/// </summary>
		List<FilePackageDef> _packages = new List<FilePackageDef>();


		public SharedXmlConfigReader( System.IO.TextReader textReader )
		{
			cfg = new SharedConfig();
			doc = XDocument.Load( textReader ); // null should never be returned, exception would be thrown insterad
#pragma warning disable CS8601 // Possible null reference assignment.
			root = doc.Element( "Shared" );
#pragma warning restore CS8601 // Possible null reference assignment.
			if ( root is null ) throw new Exception("SharedConfig missing the root element");

			loadAppDefaults();
			loadPlans();
			CheckDependencies();
			loadScripts();
			loadMachines();
			loadUnboundFiles();

			cfg.Files = _files;
		}

		AppDef readAppElement( XElement e )
		{
			AppDef a;

			// first load templates
			var templateName = X.getStringAttr( e, "Template" );
			if( !string.IsNullOrEmpty(templateName) )
			{
				XElement? te = ( from t in root.Elements( "AppTemplate" )
								where t.Attribute( "Name" )?.Value == templateName
								select t ).FirstOrDefault();

				if( te == null )
				{
					// FIXME: tog that template is missing
					var msg = String.Format( "Template '{0}' not found", templateName );
					log.ErrorFormat( msg );
					throw new ConfigurationErrorException( msg );
					//a = new AppDef();
				}
				else
				{
					a = readAppElement( te );
				}
			}
			else
			{
				a = new AppDef();
			}

			// read element content into memory, apply defaults
			var x = new
			{
				Id = e.Attribute( "AppIdTuple" )?.Value,
				ExeFullPath = e.Attribute( "ExeFullPath" )?.Value,
				StartupDir = e.Attribute( "StartupDir" )?.Value,
				CmdLineArgs = e.Attribute( "CmdLineArgs" )?.Value,
				StartupOrder = e.Attribute( "StartupOrder" )?.Value,
				Disabled = e.Attribute( "Disabled" )?.Value,
				Volatile = e.Attribute( "Volatile" )?.Value,
				ReusePrevVars = e.Attribute( "ReusePrevVars" )?.Value,
				LeaveRunningWithPrevVars = e.Attribute( "LeaveRunningWithPrevVars" )?.Value,
				RestartOnCrash = e.Attribute( "RestartOnCrash" )?.Value,
				AdoptIfAlreadyRunning = e.Attribute( "AdoptIfAlreadyRunning" )?.Value,
				PriorityClass = e.Attribute( "PriorityClass" )?.Value,
				InitCondition = e.Attribute( "InitCondition" )?.Value,
				SeparationInterval = e.Attribute( "SeparationInterval" )?.Value,
				MinKillingTime = e.Attribute( "MinKillingTime" )?.Value,
				Dependecies = e.Attribute( "Dependencies" )?.Value,
				KillTree = e.Attribute( "KillTree" )?.Value,
				KillSoftly = e.Attribute( "KillSoftly" )?.Value,
				WindowStyle = e.Attribute( "WindowStyle" )?.Value,
				WindowPos = e.Elements( "WindowPos" ),
				Restarter = e.Element( "Restarter" ),
				SoftKill = e.Element( "SoftKill" ),
				Env = e.Element( "Env" ),
				InitDetectors = e.Element( "InitDetectors" )?.Elements(),
				Groups = e.Attribute( "Groups" )?.Value,
			};

			// then overwrite templated values with current content
			if( x.Id != null ) a.Id = new AppIdTuple( x.Id );
			if( x.ExeFullPath != null ) a.ExeFullPath = x.ExeFullPath;
			if( x.StartupDir != null ) a.StartupDir = x.StartupDir;
			if( x.CmdLineArgs != null ) a.CmdLineArgs = x.CmdLineArgs;
			if( x.StartupOrder != null ) a.StartupOrder = int.Parse( x.StartupOrder );
			if( x.Disabled != null ) a.Disabled = ( int.Parse( x.Disabled ) != 0 );
			if( x.Volatile != null ) a.Volatile = ( int.Parse( x.Volatile ) != 0 );
			if( x.ReusePrevVars != null ) a.ReusePrevVars = ( int.Parse( x.ReusePrevVars ) != 0 );
			if( x.LeaveRunningWithPrevVars != null ) a.LeaveRunningWithPrevVars = ( int.Parse( x.LeaveRunningWithPrevVars ) != 0 );
			if( x.RestartOnCrash != null ) a.RestartOnCrash = ( int.Parse( x.RestartOnCrash ) != 0 );
			if( x.AdoptIfAlreadyRunning != null ) a.AdoptIfAlreadyRunning = ( int.Parse( x.AdoptIfAlreadyRunning ) != 0 );
			if( x.PriorityClass != null ) a.PriorityClass = x.PriorityClass;
			if( x.InitCondition != null ) a.InitializedCondition = x.InitCondition;
			if( x.SeparationInterval != null ) a.SeparationInterval = double.Parse( x.SeparationInterval, CultureInfo.InvariantCulture );
			if( x.MinKillingTime != null ) a.MinKillingTime = double.Parse( x.MinKillingTime, CultureInfo.InvariantCulture );
			if( x.Dependecies != null )
			{
				var deps = new List<string>();
				foreach( var d in x.Dependecies.Split( ';' ) )
				{
					var stripped = d.Trim();
					if( stripped != "" )
					{
						deps.Add( d );
					}

				}
				a.Dependencies = deps;
			}

			if( x.KillTree != null ) a.KillTree = ( int.Parse( x.KillTree ) != 0 );

			if( !String.IsNullOrEmpty( x.KillSoftly ) ) a.KillSoftly = ( int.Parse( x.KillSoftly ) != 0 );

			if( x.WindowStyle != null )
			{
				if( x.WindowStyle.ToLower() == "minimized" ) a.WindowStyle = EWindowStyle.Minimized;
				else if( x.WindowStyle.ToLower() == "maximized" ) a.WindowStyle = EWindowStyle.Maximized;
				else if( x.WindowStyle.ToLower() == "normal" ) a.WindowStyle = EWindowStyle.Normal;
				else if( x.WindowStyle.ToLower() == "hidden" ) a.WindowStyle = EWindowStyle.Hidden;
			}

			if( x.WindowPos != null )
			{
				foreach( var elem in x.WindowPos )
				{
					a.WindowPosXml.Add( elem.ToString() );
				}
			}

			if( x.Restarter != null )
			{
				a.RestarterXml = x.Restarter.ToString();
			}

			if( x.SoftKill != null )
			{
				a.SoftKillXml = x.SoftKill.ToString();
			}

			if( x.Env != null )
			{
				foreach( var elem in x.Env.Descendants() )
				{
					if( elem.Name == "Set" )
					{
						// add/overwite variable
						var variable = elem.Attribute( "Variable" )?.Value;
						var value = elem.Attribute( "Value" )?.Value;
						
						if( !string.IsNullOrEmpty(variable) && value != null )
							a.EnvVarsToSet[variable] = value;
					}

					if( elem.Name == "Local" )
					{
						// add/overwite variable
						var variable = elem.Attribute( "Variable" )?.Value;
						var value = elem.Attribute( "Value" )?.Value;

						if( !string.IsNullOrEmpty(variable) && value != null )
							a.LocalVarsToSet[variable] = value;
					}

					if( elem.Name == "Path" )
					{
						// extend
						var toAppend = elem.Attribute( "Append" )?.Value;
						if( !string.IsNullOrEmpty( toAppend ) )
						{
							if( String.IsNullOrEmpty( a.EnvVarPathToAppend ) )
							{
								a.EnvVarPathToAppend = toAppend;
							}
							else
							{
								a.EnvVarPathToAppend = a.EnvVarPathToAppend + ";" + toAppend;
							}
						}

						var toPrepend = elem.Attribute( "Prepend" )?.Value;
						if( !string.IsNullOrEmpty( toPrepend ) )
						{
							if( String.IsNullOrEmpty( a.EnvVarPathToPrepend ) )
							{
								a.EnvVarPathToPrepend = toPrepend;
							}
							else
							{
								a.EnvVarPathToPrepend = toPrepend + ";" + a.EnvVarPathToPrepend;
							}
						}
					}

				}
			}

			if( x.InitDetectors != null )
			{
				foreach( var elem in x.InitDetectors )
				{
					a.InitDetectors.Add( elem.ToString() );
				}
			}

			if( !string.IsNullOrWhiteSpace(x.Groups) )
			{
				if( !string.IsNullOrEmpty(a.Groups) ) a.Groups += ";";
				a.Groups += x.Groups;
			}

			foreach( var fileDef in loadFiles( e, a.Id.MachineId, a.Id.AppId ) )
			{
				var nonDuplFD = AddToAllFiles( fileDef ); // might return an already defined (if duplicate)
				a.Files.Add( nonDuplFD.Guid );
			}

			return a;
		}

		FileDef readFileElement( XElement e, string? machineId=null, string? appId=null, EFLookupType lookupType=EFLookupType.Path )
		{
			FileDef a = new FileDef();
			a.Guid = Guid.NewGuid();
			a.MachineId = machineId;
			a.AppId = appId;
			a.LookupType = lookupType;

			var x = new 
			{
				Id = e.Attribute( "Id" )?.Value,
				MachineId = e.Attribute( "MachineId" )?.Value,
				AppId = e.Attribute( "AppId" )?.Value,
				Path = e.Attribute( "Path" )?.Value,
				Folder = e.Attribute( "Folder" )?.Value,
				Mask = e.Attribute( "Mask" )?.Value,
			};

			if( x.Id == null )
				throw new Exception( $"File definition missing the Id attribute: {e}" );

			a.Id = x.Id;
			if( x.MachineId != null ) a.MachineId = x.MachineId;
			if( x.AppId != null ) a.AppId = x.AppId;
			a.Path = x.Path;
			a.Folder = x.Folder;
			a.Mask = x.Mask;

			return a;
		}

		void loadAppDefaults()
		{
			cfg.AppDefaults.Clear();

			var apps = from e in root.Elements( "App" )
						select e;

			foreach( var p in apps )
			{
				var app = readAppElement( p );
				cfg.AppDefaults.Add( app );
			}
		}

		void loadPlans()
		{
			var plans = from e in root.Elements( "Plan" )
						select e;

			int planIndex = 0;
			foreach( var p in plans )
			{
				planIndex++;
				var planName = p.Attribute( "Name" )?.Value;
				var startTimeout = X.getDoubleAttr( p, "StartTimeout", -1, true );

				var apps = ( from e in p.Descendants( "App" )
							 select readAppElement( e ) ).ToList();

				if( string.IsNullOrEmpty(planName) )
					throw new ConfigurationErrorException( $"Missing plan name in plan #{planIndex}");

				var groups = p.Attribute( "Groups" )?.Value ?? string.Empty;

				var applyOnStart = X.getBoolAttr( p, "ApplyOnStart", false, true );

				var applyOnSelect = X.getBoolAttr( p, "ApplyOnSelect", false, true );

				// check if everything is valid
				int index = 1;
				foreach( var a in apps )
				{
					a.PlanName = planName;

					if( string.IsNullOrEmpty(a.Id.AppId) || string.IsNullOrEmpty(a.Id.MachineId) )
					{
						throw new ConfigurationErrorException( string.Format( "App #{0} in plan '{1}' not having valid AppTupleId", index, planName ) );
					}

					if( a.ExeFullPath == null )
					{
						throw new ConfigurationErrorException( string.Format( "App #{0} in plan '{1}' not having valid ExeFullPath", index, planName ) );
					}

					index ++;
				}

				cfg.Plans.Add(
					new PlanDef()
					{
						Name = planName,
						AppDefs = apps,
						StartTimeout = startTimeout,
						Groups = groups,
						ApplyOnStart = applyOnStart,
						ApplyOnSelect = applyOnSelect
					}
				);
			}

		}

		bool AppExists( AppIdTuple id )
		{
			foreach( var pd in cfg.Plans )
			{
				foreach( var ad in pd.AppDefs )
				{
					if( ad.Id == id )
						return true;
				}
			}

			foreach( var ad in cfg.AppDefaults )
			{
				if( ad.Id == id )
					return true;
			}

			return false;
		}

		void CheckDependenciesExist( string source, IEnumerable<AppDef> appDefs )
		{
			foreach( var ad in appDefs )
			{
				if( ad.Dependencies is not null )
				{
					foreach( var depName in ad.Dependencies )
					{
						var depId = new AppIdTuple( depName );
						if( !AppExists( depId ) )
						{
		                    throw new UnknownDependencyException( $"{source}: {ad.Id}: Dependency {depName} not found." );
						}
					}
				}
			}
		}

		// Checks within a single plan only
		// Does not find cross-plan circular dependencies...
		void CheckDependenciesCircular( PlanDef planDef, AppDef ad, Dictionary<AppIdTuple, bool> depsUsed )
		{
			if( ad.Dependencies is not null )
			{
				foreach( var depName in ad.Dependencies )
				{
					var depId = new AppIdTuple( depName );
					if( depsUsed.ContainsKey( depId ) )
					{
		                throw new CircularDependencyException( $"{planDef.Name}: {ad.Id}: Circular dependency {depName} found." );
					}
					// remember this dep
					depsUsed[depId] = true;
					
					// check it recursively
					var depAppDef = planDef.AppDefs.FirstOrDefault( x => x.Id == depId );
					if( depAppDef is not null )
					{
						CheckDependenciesCircular( planDef, depAppDef, depsUsed );
					}
				}
			}
		}

		void CheckDependencies()
		{
			CheckDependenciesExist( $"AppDefaults", cfg.AppDefaults );

			// check if all dependencies mentioned exists either in a plan or in app defaults
			foreach( var pd in cfg.Plans )
			{
				CheckDependenciesExist( $"Plan {pd.Name}", pd.AppDefs );
			}

			// check circular dependency within a plan
			// WARNING: does not find cross-plan circular dependencies.. not possible to tell if such dep is a real problem or not
			foreach( var pd in cfg.Plans )
			{
				foreach( var ad in pd.AppDefs )
				{
					Dictionary<AppIdTuple, bool> depsUsed = new ();
					CheckDependenciesCircular( pd, ad, depsUsed );
				}
			}

		}

		void loadScripts()
		{
			cfg.Scripts.Clear();

			var scripts = from e in root.Elements( "Script" )
						select e;

			int index = 0;
			foreach( var p in scripts )
			{
				index++;
				var id = X.getStringAttr( p, "Name", "" );
				var file = X.getStringAttr( p, "File", "" );
				var args = X.getStringAttr( p, "Args", "" );
				var groups = X.getStringAttr( p, "Groups", "" );

				if( string.IsNullOrEmpty(id) )
					throw new ConfigurationErrorException( $"Missing script name in script #{index}");

				cfg.Scripts.Add(
					new ScriptDef()
					{
						Id = id,
						FileName = file,
						Args = args,
						Groups = groups
					}
				);
			}

		}

		List<FileDef> loadFiles( XElement root, string? machineId=null, string? appId=null )
		{
			var res = new List<FileDef>();

			var fileElems = from e in root.Elements( "File" )
						select e;
			foreach( var e in fileElems )
			{
				var fileDef = readFileElement( e, machineId, appId );
				res.Add(fileDef);
			}

			return res;
		}

		FileDef AddToAllFiles( FileDef fileDef )
		{
			// if already defined with same attribs, use the existing
			foreach( var x in _files )
			{
				if( x.SameAs( fileDef ) )
				{
					return x;
				}
			}
			// add
			_files.Add( fileDef );
			return fileDef;;
		}


		void loadMachines()
		{
			cfg.Machines.Clear();

			var machines = from e in root.Elements( "Machine" )
						select e;

			int index = 0;
			foreach( var p in machines )
			{
				index++;
				var id = X.getStringAttr( p, "Name", "" );
				var ip = X.getStringAttr( p, "IP", "" );

				if( string.IsNullOrEmpty(id) )
					throw new ConfigurationErrorException( $"Missing machine name in {p} #{index}");

				var fileDefs = new List<FileDef>();
				foreach( var fileDef in loadFiles( p, id, null ) )
				{
					// add to global list, possible reusing an already defined one if duplicated
					fileDefs.Add( AddToAllFiles( fileDef ) );
				}

				cfg.Machines.Add(
					new MachineDef()
					{
						Id = id,
						IP = ip,
						Files = (from x in fileDefs select x.Guid).ToList()
					}
				);;
			}

		}

		void loadUnboundFiles()
		{
			var fileDefs = new List<FileDef>();
			foreach( var fileDef in loadFiles( root, null, null ) )
			{
				// add to global list, possible reusing an already defined one if duplicated
				fileDefs.Add( AddToAllFiles( fileDef ) );
			}
		}



	}
}
