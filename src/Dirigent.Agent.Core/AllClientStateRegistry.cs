using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{

	/// <summary>
	/// Used by master to reflect the current state of any known application.
	/// Client states are received from clients on regular intervals.
	/// </summary>
	public class AllClientStateRegistry
	{
		private Dictionary<string, ClientState> _clientStates = new Dictionary<string, ClientState>();

		public Dictionary<string, ClientState> ClientStates => _clientStates;

		/// <param name="IP">if not null, will be updated, otherwise unchaged</param>
		public void AddOrUpdate( string id, ClientState clientState, string? IP=null )
		{
			if (_clientStates.TryGetValue( id, out var cs ))
			{
				clientState.IP = cs.IP;	// keep original ip unless a different one is explicitly specified
				if (IP != null) clientState.IP = IP;
				_clientStates[id] = clientState;
			}
			else
			{
				if (IP != null) clientState.IP = IP;
				_clientStates.Add( id, clientState );
			}
		}

		public void SetDefault( string id )
		{
			_clientStates[id] = ClientState.GetDefault();
		}
	}
}
