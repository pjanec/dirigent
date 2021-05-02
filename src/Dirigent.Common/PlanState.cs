using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent.Common
{

	/// <summary>
	/// State of an app within a plan; Master's part of app status.
	/// </summary>
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class PlanAppState
	{
		[Flags]
		enum FL
		{
			PlanApplied     = 1 << 0,
		}

		[ProtoBuf.ProtoMember( 1 )]
		FL flags;

		bool Is( FL value )
		{
			return ( flags & value ) == value;
		}

		void Set( FL value, bool toSet )
		{
			if( toSet ) flags |= value;
			else flags &= ~value;

			changed();
		}

		void changed()
		{
		}

		public bool PlanApplied
		{
			get { return Is( FL.PlanApplied ); }
			set { Set( FL.PlanApplied, value ); changed(); }
		}
	}


	/// <summary>
	/// Plan execution status
	/// </summary>
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class PlanState
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public bool Running;  // currently taking care of apps (launching, keeping alive...); mutually exclusive with Running

		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public bool Killing; //	currently killing apps; mutually exclusive with Running

		public enum EOpStatus
		{
			None,	 // plan not running, not controlling apps
			InProgress,	 // still launching
			Success,  // all apps started and initialized and running
			Failure,  // launching some of the apps failed (start failure, init failure, crash...)
			Killing	// we are killing a plan and some apps are still dying
		}

		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public EOpStatus OpStatus; // status to report to the user; determined fromthe state of contained apps


		[ProtoBuf.ProtoMember( 4 )]
		[DataMember]
		public DateTime TimeStarted; // to calculate app-start timeout causing plan failure


		[ProtoBuf.ProtoMember( 5 )]
		[DataMember]
		[MaybeNull] // when constructed without arguments by protobuf
		public Dictionary<AppIdTuple, PlanAppState> PlanAppStates;
	}


}
