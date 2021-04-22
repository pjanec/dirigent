using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Dirigent.Common
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
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class AppDef : IEquatable<AppDef>
	{
		/// <summary>
		/// Unique application name; together with MachineId makes a unique name across all applications on all machines.
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public AppIdTuple Id;

		/// <summary>
		/// What plan this appdef belongs to
		/// </summary>
		[ProtoBuf.ProtoMember(2)]
		[DataMember]
		public string? PlanName = null;

		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public string ExeFullPath = string.Empty;

		[ProtoBuf.ProtoMember( 4 )]
		[DataMember]
		public string StartupDir = string.Empty;

		[ProtoBuf.ProtoMember( 5 )]
		[DataMember]
		public string CmdLineArgs = string.Empty;

		[ProtoBuf.ProtoMember( 6 )]
		[DataMember]
		public int StartupOrder;

		/// <summary>
		/// Is the application expected to terminate automatically
		/// Such apps are not part of plan start success condition
		/// </summary>
		[ProtoBuf.ProtoMember( 7 )]
		[DataMember]
		public bool Volatile;

		[ProtoBuf.ProtoMember( 8 )]
		[DataMember]
		public bool RestartOnCrash;

		[ProtoBuf.ProtoMember( 9 )]
		[DataMember]
		public bool AdoptIfAlreadyRunning;

		[ProtoBuf.ProtoMember( 10 )]
		[DataMember]
		public string PriorityClass = string.Empty; // idle, belownormal, normal, abovenormal, high, realtime; empty = normal

		[ProtoBuf.ProtoMember( 11 )]
		[DataMember]
		public string InitializedCondition = string.Empty; //  immediate | timeout 5.23 | exitcode 0 | mutex "mymutex1"

		//[ProtoBuf.ProtoMember(??)]
		//[DataMember]
		//public List<string> Watchers = new List<string>();

		[ProtoBuf.ProtoMember( 12 )]
		[DataMember]
		public double SeparationInterval; // seconds before next app can be started on the same computer

		/// <summary>
		/// AppIds of applications that need to be initialized before this app can be started
		/// </summary>
		[ProtoBuf.ProtoMember( 13 )]
		[DataMember]
		public List<string> Dependencies = new List<string>();

		/// <summary>
		/// Shall it be processed as part of plan?
		/// </summary>
		[ProtoBuf.ProtoMember( 14 )]
		[DataMember]
		public bool Disabled;

		[ProtoBuf.ProtoMember( 15 )]
		[DataMember]
		public bool KillTree; // False = just the process started will be killed; True = all processes originating form the one started are killed also

		/// <summary>
		/// Specifies whether the process should be 'killed' through the CloseMainWindow() method (giving it a chance to handle the termination gracefully) instead of the Kill() method call.
		/// </summary>
		[ProtoBuf.ProtoMember( 16 )]
		[DataMember]
		public bool KillSoftly;

		[ProtoBuf.ProtoMember( 17 )]
		[DataMember]
		public EWindowStyle WindowStyle = EWindowStyle.NotSet;

		/// <summary>
		/// list of all <WindowPos /> XML sections as string (to be parsed later by specific app watcher code)
		/// </summary>
		[ProtoBuf.ProtoMember( 18 )]
		[DataMember]
		public List<string> WindowPosXml = new List<string>();

		/// <summary>
		/// the <Restart /> XML section as string (to be parsed later by specific app watcher code)
		/// </summary>
		[ProtoBuf.ProtoMember( 19 )]
		[DataMember]
		public string RestarterXml = String.Empty;

		/// <summary>
		/// the <KillSeq /> XML section as string (to be parsed later by Launcher code)
		/// </summary>
		[ProtoBuf.ProtoMember( 20 )]
		[DataMember]
		public string SoftKillXml = String.Empty;

		/// <summary>
		/// list of environment vars to set (in addition to inherited system environemnt)
		/// </summary>
		[ProtoBuf.ProtoMember( 21 )]
		[DataMember]
		public Dictionary<string, string> EnvVarsToSet = new Dictionary<string, string>();

		/// <summary>
		/// list of app-local vars to set (can be used in expansions for example in process exe path or command line)
		/// </summary>
		[ProtoBuf.ProtoMember( 22 )]
		[DataMember]
		public Dictionary<string, string> LocalVarsToSet = new Dictionary<string, string>();

		/// <summary>
		/// what to prepend to the PATH variable
		/// </summary>
		[ProtoBuf.ProtoMember( 23 )]
		[DataMember]
		public String EnvVarPathToPrepend = string.Empty;

		/// <summary>
		/// what to append to the PATH variable
		/// </summary>
		[ProtoBuf.ProtoMember( 24 )]
		[DataMember]
		public String EnvVarPathToAppend = string.Empty;

		/// <summary>
		/// the element within the InitDetectors section
		/// </summary>
		[ProtoBuf.ProtoMember( 25 )]
		[DataMember]
		public List<string> InitDetectors = new List<string>();


		public bool Equals( AppDef? other )
		{
			if( other is null )
				return false;

			if(
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
				true
			)
				return true;
			else
				return false;
		}

		public override bool Equals( Object? obj )
		{
			if( obj == null )
				return false;

			var typed = obj as AppDef;
			if( typed is null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Id.GetHashCode() ^ this.ExeFullPath.GetHashCode();
		}

		public static bool operator ==( AppDef t1, AppDef t2 )
		{
			if( ( object )t1 == null || ( ( object )t2 ) == null )
				return Object.Equals( t1, t2 );

			return t1.Equals( t2 );
		}

		public static bool operator !=( AppDef t1, AppDef t2 )
		{
			if( t1 is null || t2 is null )
				return !Object.Equals( t1, t2 );

			return !( t1.Equals( t2 ) );
		}


		public override string ToString()
		{
			if( string.IsNullOrEmpty(PlanName) )
				return $"{Id}";
			else
				return $"{Id}@{PlanName}";
		}
	}

}
