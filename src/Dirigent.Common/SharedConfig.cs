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
		public List<ScriptDef> SinglScripts = new List<ScriptDef>();
		public List<MachineDef> Machines = new List<MachineDef>();
		public List<VfsNodeDef> VfsNodes = new List<VfsNodeDef>(); // all Vfs nodes defined throughout the SharedConfig
		public List<AssocMenuItemDef> ToolMenuItems = new List<AssocMenuItemDef>(); // shown in main menu
	}
}
