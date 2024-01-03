using System.Collections.Generic;

namespace Dirigent
{
	/// <summary>
	/// Configuration shared by all dirigent agents. Stored on master, sent to each agent.
	/// </summary>
	public class SharedConfig
	{
		public List<AppDef> AppDefaults = new List<AppDef>();
		public List<PlanDef> Plans = new List<PlanDef>();
		public List<ScriptDef> SingleInstScripts = new List<ScriptDef>();
		public List<MachineDef> Machines = new List<MachineDef>();
		public List<VfsNodeDef> VfsNodes = new List<VfsNodeDef>(); // all Vfs nodes defined throughout the SharedConfig
		public List<AssocMenuItemDef> MainMenuItems = new List<AssocMenuItemDef>(); // shown in main menu
		public List<ActionDef> KillAllExtras = new List<ActionDef>(); // actions to run when KillAll is issued, on top of killing all running apps and plans
	}
}
