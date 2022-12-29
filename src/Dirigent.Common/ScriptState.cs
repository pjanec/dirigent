using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent
{

	public enum EScriptStatus
	{
		Unknown,
		Starting,
		Running, // Run in progress, text = status text, data = user data
		Finished, // Run finished, data = result
		Failed,  // exception thrown, data = ScriptError
		Cancelling, // Run being cancelled but still running, text & data same as for Running
		Cancelled,
	}



	//[MessagePack.MessagePackObject]
	public class ScriptState : IEquatable<ScriptState>
	{
		//[MessagePack.Key( 1 )]
		public EScriptStatus Status = EScriptStatus.Unknown;

		//[MessagePack.Key( 2 )]
		public string? Text = null;

		/// <summary>
		/// Script status info
		/// If status == Running, it is the progress info (script-specific format, usually some serialized struct).
		/// If status == Finished, it is the result (script-specific format, usually some serialized struct).
		/// If status == Failed, it is the instance of SerializedException (serialized).
		/// </summary>
		//[MessagePack.Key( 3 )]
		public byte[]? Data = null;

		public ScriptState() {}
		
		public ScriptState( EScriptStatus status, string? text=null, byte[]? data=null )
		{
			Status = status;
			Text = text;
			Data = data;
		}

		/// <summary>
		/// Is the script not yet dead?
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool IsAlive => Status == EScriptStatus.Starting || Status == EScriptStatus.Running || Status == EScriptStatus.Cancelling;

		public override string ToString()
		{
			return $"{Status} \"{Text}\" {Data?.Length} bytes";
		}

		public bool ThisEquals( ScriptState other ) =>
			this.Status == other.Status &&
			this.Text == other.Text &&
			this.Data == other.Data && // just reference equality should be enough as the serializer always creates a new array
			true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(ScriptState? o) => object.Equals(this, o);
		public static bool operator ==(ScriptState o1, ScriptState o2) => object.Equals(o1, o2);
		public static bool operator !=(ScriptState o1, ScriptState o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => Status.GetHashCode() ^ (Text?.GetHashCode() ?? 0) ^ (Data?.GetHashCode() ?? 0);
	}


}
