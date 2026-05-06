using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using System.Linq;

namespace Dirigent.Web
{
    // DTO for the client details
    public class ClientStateDetails
    {
        public bool connected { get; set; }
        public string ip { get; set; } = string.Empty;
        public string lastChange { get; set; } = string.Empty;

        public ClientStateDetails()
        {
        }

        public ClientStateDetails( Dirigent.ClientState cs )
        {
            connected = cs.Connected;
            ip = cs.IP ?? string.Empty;
            lastChange = cs.LastChange.ToString("o"); // ISO 8601 formatting for DateTime
        }
    }

    // DTO for the top-level client state response
    public class ClientState
    {
        public string id { get; set; } = string.Empty;
        public ClientStateDetails state { get; set; } = new();

        public ClientState()
        {
        }

        public ClientState( string id, Dirigent.ClientState cs )
        {
            this.id = id;
            this.state = new ClientStateDetails( cs );
        }
    }

    public class ClientApiController : WebApiController
    {
        private Master _master;

        public ClientApiController( Master master )
        {
            _master = master;
        }

        // Gets all clients states
        [Route( HttpVerbs.Get, "/clients" )]
        public async Task<IEnumerable<ClientState>> GetAllClientsState()
        {
            List<ClientState> res = new List<ClientState>();
            
            // Execute safely on the Master's thread
            var op = _master.AddSynchronousOp( () =>
            {
                res = (from kv in _master.GetAllClientsState() 
                       select new ClientState( kv.Key, kv.Value )).ToList();
            } );
            
            await op.WaitAsync();
            if( op.Exception != null ) throw op.Exception;
            
            return res;
        }

        // Gets one concrete client state
        [Route( HttpVerbs.Get, "/clients/{id}" )]
        public async Task<ClientState> GetClientState( string id )
        {
            ClientState res = new ClientState();
            
            var op = _master.AddSynchronousOp( () =>
            {
                var cs = _master.GetClientState( id );
                if( cs is null )
                    throw HttpException.NotFound();

                res = new ClientState( id, cs );
            } );
            
            await op.WaitAsync();
            if( op.Exception != null ) throw op.Exception;
            
            return res;
        }
    }
}
