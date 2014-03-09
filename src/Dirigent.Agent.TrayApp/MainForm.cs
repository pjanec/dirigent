using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.IO;

using Dirigent.Common;
using Dirigent.Agent.Core;

namespace Dirigent.Agent.TrayApp
{
    public partial class frmMain : Form
    {
        Dirigent.Agent.Core.Agent agent;
        SharedConfig scfg;
        LocalConfig lcfg;
        string machineId = "";

        void terminateFromConstructor()
        {
            Load += (s, e) => Close();
        }

        public frmMain()
        {
            InitializeComponent();
            
            lcfg = loadLocalConfig();
            
            scfg = loadSharedConfig();
            if( scfg == null )
            {
                terminateFromConstructor();
                return;
            }

            // autoconfigure if local config failed
            if( lcfg == null || lcfg.LocalMachineId == "")
            {
                machineId = MachineConfigHelper.autoconfigMachineIdFromIpAddress( scfg.Machines.Values );
                MessageBox.Show( machineId );
            }
            else
            {
                machineId = lcfg.LocalMachineId;
            }

            // find master's connection info 
            string masterIP;
            try {
                masterIP = scfg.Machines[scfg.MasterName].IpAddress;
            } catch( KeyNotFoundException ex )
            {
                showException(ex, "Configuration Error", string.Format("Could not find master name '{0} 'in machines list", scfg.MasterName) );
                terminateFromConstructor();
                return;
            }

            bool isMaster = (machineId == scfg.MasterName);
            
            // instantiate agent
            try {
                agent = new Dirigent.Agent.Core.Agent( machineId, masterIP, scfg.MasterPort, isMaster );
            } catch( Exception ex )
            {
                showException(ex, "Startup Error", "Failed to start agent.");
                terminateFromConstructor();
                return;
            }

            // load a testing plan
            agent.getControl().LoadPlan( scfg.Plans["plan1"] );
            
            // start ticking it
            tmrTick.Enabled = true;
            
        }

        void showException( Exception ex, string dialogTitle, string messageText )
        {
            MessageBox.Show(
                string.Format(
                    "{0}\n"+
                    "\n"+
                    "Exception: [{1}]\n"+
                    "{2}\n"+
                    "\n"+
                    "Stack Trace:\n{3}",
                    messageText,
                    ex.GetType().ToString(),
                    ex.Message,
                    ex.StackTrace
                ),
                "Dirigent - " + dialogTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation );
        }

        SharedConfig loadSharedConfig()
        {
            SharedXmlConfigReader cr = new SharedXmlConfigReader();
            string cfgFileName = Path.GetFullPath( "../../../../data/SharedConfig.xml" );
            try
            {
                return cr.Load( File.OpenText(cfgFileName) );
            }
            catch( Exception ex )
            {
                showException(
                    ex,
                    "Configuration Load Error",
                    string.Format("Failed to read configuration from file '{0}'.", cfgFileName)
                );
            }
            return null;
        }

        /// <summary>
        /// Returns none if config failed to load.
        /// </summary>
        /// <returns></returns>
        LocalConfig loadLocalConfig()
        {
            LocalXmlConfigReader cr = new LocalXmlConfigReader();
            string cfgFileName = Path.GetFullPath( "../../../../data/LocalConfig.xml" );
            if( !File.Exists( cfgFileName ) )
            {
                return null;
            }

            try
            {
                return cr.Load( File.OpenText(cfgFileName) );
            }
            catch( Exception ex )
            {
                showException(
                    ex,
                    "Configuration Load Error",
                    string.Format("Failed to read configuration from file '{0}'.", cfgFileName)
                );
            }
            return null;
        }

        private void tmrTick_Tick(object sender, EventArgs e)
        {
            agent.tick();
            refreshGui();
        }

        string getStatusCode( AppState st )
        {
            string stCode = "Not running";

            if( st.Running && !st.Initialized )
            {
                stCode = "Initializing";
            }
            if( st.Running && st.Initialized )
            {
                stCode = "Running";
            }
            if( st.WasLaunched && !st.Running )
            {
                stCode = "Terminated";
            }

            
            return stCode;
        }

        void refreshAppList()
        {
            var plan = agent.getControl().GetPlan();
            
            lstvApps.Items.Clear();

            if( plan != null )
            {
                foreach( AppDef a in plan.getAppDefs() )
                {
                    lstvApps.Items.Add(
                        new ListViewItem(
                            new string[]
                            {
                                a.AppIdTuple.ToString(),
                                getStatusCode( agent.getControl().GetAppState(a.AppIdTuple) )
                            }
                        )
                    );
                }
            }
        }

        void refreshGui()
        {
            refreshAppList();
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            agent.getControl().StartPlan();
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            agent.getControl().StopPlan();
        }

        private void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            agent.getControl().RestartPlan();
        }
    }
}
