using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent
{

	/// <summary>
	/// Client status known to master and shared with other participats.
	/// </summary>
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class ClientState
	{
		[MaybeNull]
		[ProtoBuf.ProtoMember( 1 )]
		public Net.ClientIdent Ident;

		[Flags]
		enum FL
		{
			Connected         = 1 << 0,
		}

		[ProtoBuf.ProtoMember( 2 )]
		FL flags;

		// UTC time of last update, recalculated to local time (as the clock migh differ on different computers)
		// On Agent, the agent's UTC time of last update
		// On Master, the UTC time of last update recalculated to master's local time
		// On Gui, the UTC time of last update recalculated to gui's local time
		[ProtoBuf.ProtoMember( 3 )]
		DateTime lastChange = DateTime.UtcNow;

		[ProtoBuf.ProtoMember( 4 )]
		string? selectedPlanName; // in what plan is selected as the current one (applies to some GUIs)

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

		/// <summary>
		/// Client is currently connected. This is updated by master.
		/// </summary>
		public bool Connected
		{
			get { return Is( FL.Connected ); }
			set { Set( FL.Connected, value ); }
		}


		/// <summary>
		/// UTC Time of the last change in reported client state.
		/// </summary>
		public DateTime LastChange
		{
			get { return lastChange; }
			set { lastChange = value; }
		}

		/// <summary>
		/// In what plan's context the app was started. Current plan for apps launched directly via LaunchApp.
		/// </summary>
		public string? SelectedPlanName
		{
			get { return selectedPlanName; }
			set { selectedPlanName = value; changed(); }
		}

		void changed()
		{
			lastChange = DateTime.UtcNow;
		}

		public bool IsOffline => DateTime.UtcNow - lastChange  > TimeSpan.FromSeconds(3); 

		public static ClientState GetDefault()
		{
			return new ClientState()
			{
				lastChange = DateTime.MinValue
			};
		}

	}


}
