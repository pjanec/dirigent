
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

namespace Dirigent.Gui.WinForms
{
	public class MainExtension
	{
		protected frmMain _form;
		protected Net.Client Client => _form.Client;
		protected IDirig Ctrl => _form.Ctrl;
		protected ReflectedStateRepo ReflStates => _form.ReflStates;
		protected List<PlanDef> PlanRepo => _form.PlanRepo;
		protected PlanDef CurrentPlan { get { return _form.CurrentPlan; } set { _form.CurrentPlan = value; } }
		protected List<ScriptDef> ScriptRepo => _form.ScriptRepo;


		protected readonly Bitmap _iconStart = WFT.ResizeImage( new Bitmap( Resources.play ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconStop = WFT.ResizeImage( new Bitmap( Resources.stop ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconKill = WFT.ResizeImage( new Bitmap( Resources.delete ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconRestart = WFT.ResizeImage( new Bitmap( Resources.refresh ), new Size( 20, 20 ) );


		public MainExtension( frmMain form )
		{
			_form = form;
		}

	}
}
