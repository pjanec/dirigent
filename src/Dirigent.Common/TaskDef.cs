using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Dirigent
{

	/// <summary>
	/// Definition of a script/task in shared config. Can be found in global section, inside MachineDef, inside TaskDef, inside FileDef, inside PackageDef...
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class DTaskDef : IEquatable<DTaskDef>
	{
		/// <summary>
		/// Unique text name used to identifying this task definition within its container (global, app...)
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public string Id = string.Empty;

		/// <summary>
		/// Machine the task belongs to. Null if global.
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public string? MachineId;

		/// <summary>
		/// App the task belongs to. Used only if the task is bound to an app.
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public string? AppId;

		[ProtoBuf.ProtoMember( 4 )]
		[DataMember]
		public string? FileId;

		[ProtoBuf.ProtoMember( 5 )]
		[DataMember]
		public string? FilePackageId;

		/// <summary>
		/// Display name for a task (to be shown in menus etc.)
		/// </summary>
		[ProtoBuf.ProtoMember( 6 )]
		public string DisplayName = string.Empty;

		/// <summary>
		/// What script to run for this task.
		/// Just the base name with optional relative path (like "MyTask1", "MyTasks/Task1" etc.)
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		public string ScriptName = string.Empty;

		/// <summary>
		/// Absolute path to the C# script folder where the script files are located.
		/// Found from the directory search.
		/// </summary>
		/// <remarks>
		/// The script file names are constructed as
		///   [ScriptFolder]/[ScriptName].cs  ..... for non-distributed script running on master only
		///   [ScriptFolder]/[ScriptName].Controller.cs ... the part of the distributed task running on master
		///   [ScriptFolder]/[ScriptName].Worker.cs .... the part of of distributed part running on agent
		/// </remarks>
		[ProtoBuf.ProtoMember(5)]
		public string ScriptFolder = string.Empty;

		/// <summary>
		/// Args passed to the task class instance
		/// </summary>
		[ProtoBuf.ProtoMember( 6 )]
		public string Args = string.Empty;

		// semicolon separated list of "paths" like "main/examples;"GUI might use this for showing scripts in a folder tree
		[ProtoBuf.ProtoMember( 7 )]
		public string Groups = string.Empty;

		/// <summary>
		/// list of script-local vars to set (can be used in expansions for example in process exe path or command line)
		/// </summary>
		[ProtoBuf.ProtoMember( 8 )]
		[DataMember]
		public Dictionary<string, string> LocalVarsToSet = new Dictionary<string, string>();



		public bool Equals( DTaskDef? other )
		{
			if( other is null )
				return false;

			if(
				this.Id == other.Id &&
				this.DisplayName == other.DisplayName &&
				this.ScriptName == other.ScriptName &&
				this.ScriptFolder == other.ScriptFolder &&
				this.Args == other.Args &&
				this.Groups == other.Groups &&
				this.LocalVarsToSet.DictionaryEqual( other.LocalVarsToSet ) &&
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

			var typed = obj as DTaskDef;
			if( typed is null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Id.GetHashCode();
		}

		public static bool operator ==( DTaskDef t1, DTaskDef t2 )
		{
			if( ( object )t1 == null || ( ( object )t2 ) == null )
				return Object.Equals( t1, t2 );

			return t1.Equals( t2 );
		}

		public static bool operator !=( DTaskDef t1, DTaskDef t2 )
		{
			if( t1 is null || t2 is null )
				return !Object.Equals( t1, t2 );

			return !( t1.Equals( t2 ) );
		}


		public override string ToString()
		{
			return $"{Id}";
		}
	}

}
