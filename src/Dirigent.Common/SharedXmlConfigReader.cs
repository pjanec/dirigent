using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using Dirigent.Common;

using X = Dirigent.Common.XmlConfigReaderUtils;
using System.Diagnostics;

namespace Dirigent.Common
{

	public class SharedXmlConfigReader
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public SharedConfig cfg;
		XDocument doc;

		public SharedXmlConfigReader( System.IO.TextReader textReader )
		{
			cfg = new SharedConfig();
			doc = XDocument.Load( textReader );

			loadAppDefaults();
			loadPlans();
			//loadMachines();
			//loadMaster();
		}

		AppDef readAppElement( XElement e )
		{
			AppDef a;

			// first load templates
			var templateName = X.getStringAttr( e, "Template" );
			if( !string.IsNullOrEmpty(templateName) )
			{
				XElement? te = ( from t in doc?.Element( "Shared" )?.Elements( "AppTemplate" )
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
				RestartOnCrash = e.Attribute( "RestartOnCrash" )?.Value,
				AdoptIfAlreadyRunning = e.Attribute( "AdoptIfAlreadyRunning" )?.Value,
				PriorityClass = e.Attribute( "PriorityClass" )?.Value,
				InitCondition = e.Attribute( "InitCondition" )?.Value,
				SeparationInterval = e.Attribute( "SeparationInterval" )?.Value,
				Dependecies = e.Attribute( "Dependencies" )?.Value,
				KillTree = e.Attribute( "KillTree" )?.Value,
				KillSoftly = e.Attribute( "KillSoftly" )?.Value,
				WindowStyle = e.Attribute( "WindowStyle" )?.Value,
				WindowPos = e.Elements( "WindowPos" ),
				Restarter = e.Element( "Restarter" ),
				SoftKill = e.Element( "SoftKill" ),
				Env = e.Element( "Env" ),
				InitDetectors = e.Element( "InitDetectors" )?.Elements(),
			};

			// then overwrite templated values with current content
			if( x.Id != null ) a.Id = new AppIdTuple( x.Id );
			if( x.ExeFullPath != null ) a.ExeFullPath = x.ExeFullPath;
			if( x.StartupDir != null ) a.StartupDir = x.StartupDir;
			if( x.CmdLineArgs != null ) a.CmdLineArgs = x.CmdLineArgs;
			if( x.StartupOrder != null ) a.StartupOrder = int.Parse( x.StartupOrder );
			if( x.Disabled != null ) a.Disabled = ( int.Parse( x.Disabled ) != 0 );
			if( x.Volatile != null ) a.Volatile = ( int.Parse( x.Volatile ) != 0 );
			if( x.RestartOnCrash != null ) a.RestartOnCrash = ( int.Parse( x.RestartOnCrash ) != 0 );
			if( x.AdoptIfAlreadyRunning != null ) a.AdoptIfAlreadyRunning = ( int.Parse( x.AdoptIfAlreadyRunning ) != 0 );
			if( x.PriorityClass != null ) a.PriorityClass = x.PriorityClass;
			if( x.InitCondition != null ) a.InitializedCondition = x.InitCondition;
			if( x.SeparationInterval != null ) a.SeparationInterval = double.Parse( x.SeparationInterval, CultureInfo.InvariantCulture );
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

			return a;
		}

		void loadAppDefaults()
		{
			var appDefaultsElem = (from e in doc.Element( "Shared" )?.Descendants( "AppDefaults" ) select e).FirstOrDefault();
			
			cfg.AppDefaults = (
				from e in appDefaultsElem?.Descendants( "App" )
				select readAppElement( e )
							  ).ToList();
		}

		void loadPlans()
		{
			var plans = from e in doc.Element( "Shared" )?.Descendants( "Plan" )
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
					StartTimeout = startTimeout
				}
				);
			}

		}

		//MachineDef readMachineElement( XElement e )
		//{
		//    MachineDef m = new MachineDef();
		//    m.MachineId = X.getStringAttr(e, "Name");
		//    m.IpAddress = X.getStringAttr(e, "IpAddress");
		//    return m;
		//}

		//void loadMachines()
		//{
		//    var machines = from m in doc.Element("Shared").Descendants("Machine")
		//                 select readMachineElement(m);

		//    foreach( var ma in machines )
		//    {
		//        cfg.Machines.Add( ma.MachineId, ma );
		//    }
		//}

		//void loadMaster()
		//{
		//    var master = doc.Element("Shared").Element("Master");
		//    cfg.MasterPort = X.getIntAttr( master, "Port" );
		//    cfg.MasterName = X.getStringAttr( master, "Name" );
		//}

		//void loadLocalMachineId()
		//{
		//    var master = doc.Element("Shared").Element("Local");
		//    cfg.MasterPort = X.getIntAttr( master, "MasterPort" );
		//    cfg.MasterName = X.getStringAttr( master, "MasterName" );
		//}

	}
}
