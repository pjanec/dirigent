using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent
{

	/// <summary>
	/// State of an app within a plan; Master's part of app status.
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class PlanAppState
	{
		[Flags]
		enum FL
		{
			PlanApplied     = 1 << 0,
		}

		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class PlanState
	{
		//[MessagePack.Key( 1 )]
		public bool Running;  // currently taking care of apps (launching, keeping alive...); mutually exclusive with Running

		//[MessagePack.Key( 2 )]
		public bool Killing; //	currently killing apps; mutually exclusive with Running

		public enum EOpStatus
		{
			None,	 // plan not running, not controlling apps
			InProgress,	 // still launching
			Success,  // all apps started and initialized and running
			Failure,  // launching some of the apps failed (start failure, init failure, crash...)
			Killing	// we are killing a plan and some apps are still dying
		}

		//[MessagePack.Key( 3 )]
		public EOpStatus OpStatus; // status to report to the user; determined fromthe state of contained apps


		//[MessagePack.Key( 4 )]
		public DateTime TimeStarted; // to calculate app-start timeout causing plan failure


		//[MessagePack.Key( 5 )]
		[MaybeNull] // when constructed without arguments by protobuf
		public Dictionary<AppIdTuple, PlanAppState> PlanAppStates;
	}


}
