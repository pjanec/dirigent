using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dirigent.Common;

namespace Dirigent.Agent
{

	/// <summary>
	/// Description of applications's current state
	/// </summary>
	public class AppDefaults
	{
		public AppDef AppDef;

		public AppDefaults( AppDef appDef )
		{
			AppDef = appDef;
		}
	}


	/// <summary>
	/// Used by master and by GUI to reflect the current state of any known application.
	/// App states are received from owning agents on change.
	/// App Defs are sent to owning agents on change.
	/// </summary>
	public class AppDefaultsRegistry
	{
		private Dictionary<AppIdTuple, AppDefaults> _apps = new Dictionary<AppIdTuple, AppDefaults>();

	}
}
