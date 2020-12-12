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
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        SharedConfig cfg;
        XDocument doc;

        public SharedConfig Load( System.IO.TextReader textReader )
        {
            cfg = new SharedConfig();
            doc = XDocument.Load(textReader);

            
            loadPlans();
            //loadMachines();
            //loadMaster();

            return cfg;
        }

        AppDef readAppElement( XElement e )
        {
            AppDef a;

            // first load templates
            var templateName = X.getStringAttr(e, "Template");
            if( templateName != "" )
            {
                XElement te = (from t in doc.Element("Shared").Elements("AppTemplate")
                        where (string) t.Attribute("Name") == templateName
                        select t).FirstOrDefault();

                if( te == null )
                {
                    // FIXME: tog that template is missing
                    var msg = String.Format("Template '{0}' not found", templateName);
                    log.ErrorFormat(msg);
                    throw new ConfigurationErrorException(msg);
                    a = new AppDef();
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
            var x = new {
                AppIdTuple = (string) e.Attribute("AppIdTuple"),
                ExeFullPath = (string) e.Attribute("ExeFullPath"),
                StartupDir = (string) e.Attribute("StartupDir"),
                CmdLineArgs = (string) e.Attribute("CmdLineArgs"),
                StartupOrder = (string) e.Attribute("StartupOrder"),
                Disabled = (string)e.Attribute("Disabled"),
				Volatile = (string) e.Attribute("Volatile"),
                RestartOnCrash = (string) e.Attribute("RestartOnCrash"),
                AdoptIfAlreadyRunning = (string) e.Attribute("AdoptIfAlreadyRunning"),
                InitCondition = (string) e.Attribute("InitCondition"),
                SeparationInterval = (string) e.Attribute("SeparationInterval"),
                Dependecies = (string) e.Attribute("Dependencies"),
                KillTree = (string)e.Attribute("KillTree"),
                KillSoftly = (string)e.Attribute("KillSoftly"),
                WindowStyle = (string)e.Attribute("WindowStyle"),
                WindowPos = e.Elements("WindowPos"),
                Restarter = e.Element("Restarter"),
                Env = e.Element("Env"),
                InitDetectors = e.Element("InitDetectors") != null ? e.Element("InitDetectors").Elements() : null,
            };

            // then overwrite templated values with current content
            if( x.AppIdTuple != null ) a.AppIdTuple = new AppIdTuple( x.AppIdTuple );
            if( x.ExeFullPath != null ) a.ExeFullPath = x.ExeFullPath;
            if( x.StartupDir != null ) a.StartupDir = x.StartupDir;
            if( x.CmdLineArgs != null ) a.CmdLineArgs = x.CmdLineArgs;
            if( x.StartupOrder != null ) a.StartupOrder = int.Parse( x.StartupOrder );
            if( x.Disabled != null ) a.Disabled = (int.Parse( x.Disabled ) != 0);
            if( x.Volatile != null ) a.Volatile = (int.Parse( x.Volatile ) != 0);
            if( x.RestartOnCrash != null ) a.RestartOnCrash = (int.Parse( x.RestartOnCrash ) != 0);
            if( x.AdoptIfAlreadyRunning != null ) a.AdoptIfAlreadyRunning = (int.Parse( x.AdoptIfAlreadyRunning ) != 0);
            if( x.InitCondition != null ) a.InitializedCondition = x.InitCondition;
            if( x.SeparationInterval != null ) a.SeparationInterval = double.Parse(x.SeparationInterval, CultureInfo.InvariantCulture );
            if (x.Dependecies != null)
            {
                var deps = new List<string>();
                foreach( var d in x.Dependecies.Split(';'))
                {
                    var stripped = d.Trim();
                    if( stripped != "" )
                    {
                        deps.Add( d );
                    }

                }
                a.Dependencies = deps;
            }

            if (x.KillTree != null) a.KillTree = (int.Parse(x.KillTree) != 0);

            if (!String.IsNullOrEmpty(x.KillSoftly)) a.KillSoftly = (int.Parse(x.KillSoftly) != 0);

            if (x.WindowStyle != null)
            {
                if (x.WindowStyle.ToLower() == "minimized") a.WindowStyle = EWindowStyle.Minimized;
                else
                if (x.WindowStyle.ToLower() == "maximized") a.WindowStyle = EWindowStyle.Maximized;
                else
                if (x.WindowStyle.ToLower() == "normal") a.WindowStyle = EWindowStyle.Normal;
                else
                if (x.WindowStyle.ToLower() == "hidden") a.WindowStyle = EWindowStyle.Hidden;
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

            if( x.Env != null )
            {
                foreach( var elem in x.Env.Descendants())
                {
					if (elem.Name == "Set")
					{
						// add/overwite variable
						var variable = (string) elem.Attribute("Variable");
						var value = (string) elem.Attribute("Value");
						a.EnvVarsToSet[variable] = value;
					}
				
					if (elem.Name == "Path")
					{
						// extend
						var toAppend = (string) elem.Attribute("Append");
						if (!string.IsNullOrEmpty(toAppend))
						{
							if (String.IsNullOrEmpty(a.EnvVarPathToAppend))
							{
								a.EnvVarPathToAppend = toAppend;
							}
							else
							{
								a.EnvVarPathToAppend = a.EnvVarPathToAppend + ";" + toAppend;
							}
						}

						var toPrepend = (string) elem.Attribute("Prepend");
						if (!string.IsNullOrEmpty(toPrepend))
						{
							if (String.IsNullOrEmpty(a.EnvVarPathToPrepend))
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

        void loadPlans()
        {
            var plans = from e in doc.Element("Shared").Descendants("Plan")
                         select e;

            foreach( var p in plans )
            {
                var planName = (string) p.Attribute("Name");
				var startTimeout = X.getDoubleAttr(p, "StartTimeout", -1, true);

                var apps = (from e in p.Descendants("App")
                            select readAppElement( e )).ToList();
                
                // check if everything is valid
                int index = 1;
                foreach( var a in apps )
                {
                    if( a.AppIdTuple == null )
                    {
                        throw new ConfigurationErrorException(string.Format("App #{0} in plan '{1}' not having valid AppTupleId", index, planName));
                    }

                    if( a.ExeFullPath == null )
                    {
                        throw new ConfigurationErrorException(string.Format("App #{0} in plan '{1}' not having valid ExeFullPath", index, planName));
                    }

                    index ++;
                }
                
                cfg.Plans.Add(
                    new LaunchPlan(
                        planName,
                        apps,
						startTimeout
                    )
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
