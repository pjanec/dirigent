
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

using System.IO;
using Dirigent.Gui.WinForms.Properties;
using System.Threading;

namespace Dirigent.Gui.WinForms
{
	public class MainExtension
	{
		protected frmMain _form;
		protected GuiCore _core;
		protected Net.Client Client => _core.Client;
		protected IDirig Ctrl => _core.Ctrl;
		protected IDirigAsync CtrlAsync => _core.CtrlAsync;
		protected ReflectedStateRepo ReflStates => _core.ReflStates;
		protected List<PlanDef> PlanRepo => _core.PlanRepo;
		protected PlanDef CurrentPlan { get { return _core.CurrentPlan; } set { _core.CurrentPlan = value; } }
		protected List<ScriptDef> ScriptRepo => _core.ScriptRepo;

		protected MenuBuilder _menuBuilder;

		protected readonly Bitmap _iconStart = WFT.ResizeImage( new Bitmap( Resources.play ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconStop = WFT.ResizeImage( new Bitmap( Resources.stop ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconKill = WFT.ResizeImage( new Bitmap( Resources.delete ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconRestart = WFT.ResizeImage( new Bitmap( Resources.refresh ), new Size( 20, 20 ) );


		public MainExtension( frmMain form, GuiCore core )
		{
			_form = form;
			_core = core;
			_menuBuilder = new MenuBuilder( _core );
		}


	}
}
