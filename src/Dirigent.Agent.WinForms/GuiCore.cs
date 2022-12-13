using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using log4net;

namespace Dirigent.Gui.WinForms
{
	public class GuiCore : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

		public bool AllowLocalIfDisconnected { get; private set; }
		private AppConfig _ac;

		public IDirig Ctrl { get; private set; }
		public IDirigAsync CtrlAsync { get; private set; }

		private string _machineId; // empty if GUI not running as part of local agent
		public string MachineId => _machineId;
		private Net.ClientIdent _clientIdent; // name of the network client; messages are marked with that

		public List<PlanDef> PlanRepo { get; private set; }

		public List<ScriptDef> ScriptRepo { get; private set; }

		public Net.Client Client { get; private set; }


		public ReflectedStateRepo ReflStates { get; private set; }

		public PlanDef CurrentPlan { get; set; }

        /// <summary>
		/// Dirigent internals vars that can be used for expansion inside process exe paths, command line...)
		/// </summary>
		private Dictionary<string, string> _internalVars = new ();
		private SharedContext _sharedContext; // necessary for launching tools
		private ToolsRegistry _toolsReg;
		public ToolsRegistry ToolsRegistry => _toolsReg;

		public ScriptFactory ScriptFactory;
		public SynchronousOpProcessor SyncOps { get; private set; }
		private LocalScriptRegistry _localScripts;

		public Action<Net.Message> IncomingMessage;


		public GuiCore(
			AppConfig ac,
			string machineId // empty if no local agent was started with the GUI
		)
		{
			_ac = ac;
			_machineId = machineId; // FIXME: this is only valid if we are running a local agent! How do we know??
			_clientIdent = new Net.ClientIdent() { Sender = Guid.NewGuid().ToString(), SubscribedTo = Net.EMsgRecipCateg.Gui };
			AllowLocalIfDisconnected = true;

			log.Debug( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );

			PlanRepo = new List<PlanDef>();
			ScriptRepo = new List<ScriptDef>();

			Client = new Net.Client( _clientIdent, ac.MasterIP, ac.MasterPort, autoConn: true );
			Client.MessageReceived += OnMessage;
			ReflStates = new ReflectedStateRepo( Client, machineId );
			
			SyncOps = new SynchronousOpProcessor();

			Ctrl = ReflStates;
			CtrlAsync = new SynchronousIDirig( Ctrl, SyncOps );

			// load tools from local config
			InitFromLocalConfig( machineId );			

			ScriptFactory = new ScriptFactory();
			_localScripts = new LocalScriptRegistry( ReflStates, ScriptFactory, SyncOps );
			
			bool firstGotPlans = true;
			ReflStates.OnPlansReceived += () =>
			{
				if( firstGotPlans )
				{
					SelectPlan( ac.StartupPlan );
				}
				firstGotPlans = false;

				// udate current plan reference in case the plan def has changed
				if( CurrentPlan is not null )
				{
					CurrentPlan = ReflStates.GetPlanDef( CurrentPlan.Name );
				}
			};


			ReflStates.OnScriptsReceived += () =>
			{
			};
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;


			_localScripts.Dispose();
			
			if( Client is not null )
			{
				Client.MessageReceived -= OnMessage;
				Client.Dispose();
				Client = null;
			}
		}

		void InitFromLocalConfig( string machineId )
		{
			// load the local config file
			if( string.IsNullOrEmpty( _ac.LocalCfgFileName ) )
				return;

			var fullPath = Path.GetFullPath( _ac.LocalCfgFileName );
			log.DebugFormat( "Loading local config file '{0}'", fullPath );
			var localConfig = new LocalConfigReader( File.OpenText( fullPath ), machineId ).Config;

			
			_sharedContext = new SharedContext(
				PathUtils.GetRootForRelativePaths( _ac.LocalCfgFileName, _ac.RootForRelativePaths ),
				_internalVars,
				new AppInitializedDetectorFactory(),
				Client
			);

			_toolsReg = new ToolsRegistry( _sharedContext, localConfig.Tools, ReflStates.FileRegistry, ReflStates.Scripts );
		}

		void OnMessage( Net.Message msg )
		{
			switch( msg )
			{
				case Net.StartScriptMessage m:
				{
					_localScripts.Start( m.Instance, m.ScriptName, m.SourceCode, m.Args, m.Title );
					break;
				}

				case Net.KillScriptMessage m:
				{
					_localScripts.Stop( m.Instance );
					break;
				}

				// note: ScriptStateMessage handling is done in ReflectedStateRepo
			}
		}

		public void Tick()
		{
			Client.Tick();
			_toolsReg?.Tick();
		}

		public void SelectPlan( string planName )
		{
			CurrentPlan = Ctrl.GetPlanDef( planName );
			Ctrl.Send( new Net.SelectPlanMessage( Ctrl.Name, CurrentPlan is null ? string.Empty : CurrentPlan.Name ) );
		}

		
		
	}
}
