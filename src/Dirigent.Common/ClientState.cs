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
	//[MessagePack.MessagePackObject]
	public class ClientState
	{
		[MaybeNull]
		//[MessagePack.Key( 1 )]
		public Net.ClientIdent Ident;

		[Flags]
		public enum FL
		{
			Connected         = 1 << 0,
		}

		//[MessagePack.Key( 2 )]
		public FL _flags;  // must be public so tham MessagePack wants to serialize it:-(

		// UTC time of last update, recalculated to local time (as the clock migh differ on different computers)
		// On Agent, the agent's UTC time of last update
		// On Master, the UTC time of last update recalculated to master's local time
		// On Gui, the UTC time of last update recalculated to gui's local time
		//[MessagePack.Key( 3 )]
		public DateTime _lastChange = DateTime.UtcNow;

		//[MessagePack.Key( 4 )]
		public string? _selectedPlanName; // in what plan is selected as the current one (applies to some GUIs)

		//[MessagePack.Key( 5 )]
		public string? IP; // ip address of the client (determined by master from dirigent's TCP connection)

		bool Is( FL value )
		{
			return ( _flags & value ) == value;
		}

		void Set( FL value, bool toSet )
		{
			if( toSet ) _flags |= value;
			else _flags &= ~value;

			changed();
		}

		/// <summary>
		/// Client is currently connected. This is updated by master.
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool Connected
		{
			get { return Is( FL.Connected ); }
			set { Set( FL.Connected, value ); }
		}


		/// <summary>
		/// UTC Time of the last change in reported client state.
		/// </summary>
		[MessagePack.IgnoreMember]
		public DateTime LastChange
		{
			get { return _lastChange; }
			set { _lastChange = value; }
		}

		/// <summary>
		/// In what plan's context the app was started. Current plan for apps launched directly via LaunchApp.
		/// </summary>
		[MessagePack.IgnoreMember]
		public string? SelectedPlanName
		{
			get { return _selectedPlanName; }
			set { _selectedPlanName = value; changed(); }
		}

		void changed()
		{
			_lastChange = DateTime.UtcNow;
		}

		public static ClientState GetDefault()
		{
			return new ClientState()
			{
				_lastChange = DateTime.MinValue
			};
		}

	}


}
