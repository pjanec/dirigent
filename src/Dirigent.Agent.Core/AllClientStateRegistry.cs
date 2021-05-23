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
		//public class ClientInfo
		//{
		//	public ClientState State;
		//	public Net.ClientIdent Ident;
		//}

		private Dictionary<string, ClientState> _clientStates = new Dictionary<string, ClientState>();

		public Dictionary<string, ClientState> ClientStates => _clientStates;

		public void AddOrUpdate( string id, ClientState clientState )
		{
			_clientStates[id] = clientState;
		}

		public void SetDefault( string id )
		{
			_clientStates[id] = ClientState.GetDefault();
		}
	}
}
