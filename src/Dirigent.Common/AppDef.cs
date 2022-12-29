using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Dirigent
{

	public enum EWindowStyle
	{
		NotSet,
		Normal,
		Minimized,
		Maximized,
		Hidden
	}

	/// <summary>
	/// Definition of an application in a launch plan
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class AppDef : IEquatable<AppDef>
	{
		/// <summary>
		/// Unique application name; together with MachineId makes a unique name across all applications on all machines.
		/// </summary>
		//[MessagePack.Key( 1 )]
		public AppIdTuple Id;

		/// <summary>
		/// What plan this appdef belongs to
		/// </summary>
		//[MessagePack.Key(2)]
		public string? PlanName = null;

		//[MessagePack.Key( 3 )]
		public string ExeFullPath = string.Empty;

		//[MessagePack.Key( 4 )]
		public string StartupDir = string.Empty;

		//[MessagePack.Key( 5 )]
		public string CmdLineArgs = string.Empty;

		//[MessagePack.Key( 6 )]
		public int StartupOrder;

		/// <summary>
		/// Is the application expected to terminate automatically
		/// Such apps are not part of plan start success condition
		/// </summary>
		//[MessagePack.Key( 7 )]
		public bool Volatile;

		//[MessagePack.Key( 8 )]
		public bool RestartOnCrash;

		//[MessagePack.Key( 9 )]
		public bool AdoptIfAlreadyRunning;

		//[MessagePack.Key( 10 )]
		public string PriorityClass = string.Empty; // idle, belownormal, normal, abovenormal, high, realtime; empty = normal

		//[MessagePack.Key( 11 )]
		public string InitializedCondition = string.Empty; //  immediate | timeout 5.23 | exitcode 0 | mutex "mymutex1"

		////[MessagePack.Key(??)]
		//public List<string> Watchers = new List<string>();

		//[MessagePack.Key( 12 )]
		public double SeparationInterval; // seconds before next app can be started on the same computer

		/// <summary>
		/// AppIds of applications that need to be initialized before this app can be started
		/// </summary>
		//[MessagePack.Key( 13 )]
		public List<string> Dependencies = new List<string>();

		/// <summary>
		/// Shall it be processed as part of plan?
		/// </summary>
		//[MessagePack.Key( 14 )]
		public bool Disabled;

		//[MessagePack.Key( 15 )]
		public bool KillTree; // False = just the process started will be killed; True = all processes originating form the one started are killed also

		/// <summary>
		/// Specifies whether the process should be 'killed' through the CloseMainWindow() method (giving it a chance to handle the termination gracefully) instead of the Kill() method call.
		/// </summary>
		//[MessagePack.Key( 16 )]
		public bool KillSoftly;

		//[MessagePack.Key( 17 )]
		public EWindowStyle WindowStyle = EWindowStyle.NotSet;

		/// <summary>
		/// list of all <WindowPos /> XML sections as string (to be parsed later by specific app watcher code)
		/// </summary>
		//[MessagePack.Key( 18 )]
		public List<string> WindowPosXml = new List<string>();

		/// <summary>
		/// the <Restart /> XML section as string (to be parsed later by specific app watcher code)
		/// </summary>
		//[MessagePack.Key( 19 )]
		public string RestarterXml = String.Empty;

		/// <summary>
		/// the <KillSeq /> XML section as string (to be parsed later by Launcher code)
		/// </summary>
		//[MessagePack.Key( 20 )]
		public string SoftKillXml = String.Empty;

		/// <summary>
		/// list of environment vars to set (in addition to inherited system environemnt)
		/// </summary>
		//[MessagePack.Key( 21 )]
		public Dictionary<string, string> EnvVarsToSet = new Dictionary<string, string>();

		/// <summary>
		/// list of app-local vars to set (can be used in expansions for example in process exe path or command line)
		/// </summary>
		//[MessagePack.Key( 22 )]
		public Dictionary<string, string> LocalVarsToSet = new Dictionary<string, string>();

		/// <summary>
		/// what to prepend to the PATH variable
		/// </summary>
		//[MessagePack.Key( 23 )]
		public String EnvVarPathToPrepend = string.Empty;

		/// <summary>
		/// what to append to the PATH variable
		/// </summary>
		//[MessagePack.Key( 24 )]
		public String EnvVarPathToAppend = string.Empty;

		/// <summary>
		/// the element within the InitDetectors section
		/// </summary>
		//[MessagePack.Key( 25 )]
		public List<string> InitDetectors = new List<string>();

		// semicolon separated list of "paths" like "main/examples;"GUI might use this for showing items in a folder tree
		//[MessagePack.Key( 26 )]
		public string Groups = string.Empty;

		//[MessagePack.Key( 27 )]
		public double MinKillingTime; // min seconds before reporting "killed" after the kill operation

		/// <summary>
		/// Specifies whether on StartApp the process should be restarted with new env vars if it is already running with a different set of explicit env vars.
		/// </summary>
		//[MessagePack.Key( 28 )]
		public bool LeaveRunningWithPrevVars;

		/// <summary>
		/// If true, removes cached env vars specified for the previous runs. Will use just those defined in AppDef and those explicitly specified as part of StartApp command.
		/// </summary>
		//[MessagePack.Key( 29 )]
		public bool ReusePrevVars;

		/// <summary>
		/// Name of the network service the app is using
		/// </summary>
		//[MessagePack.Key( 32 )]
		public string Service = string.Empty;

		/// <summary>
		/// What icon to show for this app
		/// </summary>
		//[MessagePack.Key( 33 )]
		public string IconFile = string.Empty;

		//[MessagePack.Key( 34 )]
		public List<ActionDef> Actions = new List<ActionDef>();

		/// <summary>
		/// Files/folders/packages associated with the app
		/// </summary>
		//[MessagePack.Key( 35 )]
		public List<VfsNodeDef> VfsNodes = new List<VfsNodeDef>();


		public bool ThisEquals( AppDef other ) =>
				this.Id == other.Id &&
				this.PlanName == other.PlanName &&
				this.ExeFullPath == other.ExeFullPath &&
				this.StartupDir == other.StartupDir &&
				this.CmdLineArgs == other.CmdLineArgs &&
				this.StartupOrder == other.StartupOrder &&
				this.Volatile == other.Volatile &&
				this.RestartOnCrash == other.RestartOnCrash &&
				this.AdoptIfAlreadyRunning == other.AdoptIfAlreadyRunning &&
				this.PriorityClass == other.PriorityClass &&
				this.InitializedCondition == other.InitializedCondition &&
				this.SeparationInterval == other.SeparationInterval &&
				this.MinKillingTime == other.MinKillingTime &&
				( // either both dependecies are null or they are the same list
					( this.Dependencies == null && other.Dependencies == null )
					||
					(
						this.Dependencies != null &&
						other.Dependencies != null &&
						this.Dependencies.SequenceEqual( other.Dependencies )
					)
				) &&
				this.WindowStyle == other.WindowStyle &&
				this.WindowPosXml.SequenceEqual( other.WindowPosXml ) &&
				this.RestarterXml == other.RestarterXml &&
				this.SoftKillXml == other.SoftKillXml &&
				this.EnvVarsToSet.DictionaryEqual( other.EnvVarsToSet ) &&
				this.LocalVarsToSet.DictionaryEqual( other.LocalVarsToSet ) &&
				this.EnvVarPathToPrepend == other.EnvVarPathToPrepend &&
				this.EnvVarPathToAppend == other.EnvVarPathToAppend &&
				this.InitDetectors.SequenceEqual( other.InitDetectors ) &&
				//this.Watchers.SequenceEqual(other.Watchers) &&
				this.LeaveRunningWithPrevVars == other.LeaveRunningWithPrevVars &&
				this.ReusePrevVars == other.ReusePrevVars &&
				this.Service == other.Service &&
				this.IconFile == other.IconFile &&
				this.Actions.SequenceEqual( other.Actions ) &&
				this.VfsNodes.SequenceEqual( other.VfsNodes ) &&
				true
				;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(AppDef? o) => object.Equals(this, o);
		public static bool operator ==(AppDef o1, AppDef o2) => object.Equals(o1, o2);
		public static bool operator !=(AppDef o1, AppDef o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => Id.GetHashCode();


		public override string ToString()
		{
			if( string.IsNullOrEmpty(PlanName) )
				return $"{Id}";
			else
				return $"{Id}@{PlanName}";
		}

		public AppDef Clone()
		{
			var stream = new System.IO.MemoryStream( 16000 );
			MessagePack.MessagePackSerializer.Serialize( stream, this );
			stream.Seek(0, System.IO.SeekOrigin.Begin);
			return MessagePack.MessagePackSerializer.Deserialize<AppDef>( stream );
		}

		
	}

}
