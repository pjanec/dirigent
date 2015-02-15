using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

using Dirigent.Common;

namespace Dirigent.Net
{
    // the callback is not used at the moment as clients activelly poll the messages from the server
    // (same way as using remoting)
    public interface IDirigentMasterContractCallback
    {
        [OperationContract(IsOneWay = true)]
        void MessageFromServer(  Message msg );
    }

    /// <summary>
    /// Dirigenr Master is a simple message broker forwarding each received message to all connected clients;
    /// Clients have to activaly poll the messages from the service.
    /// </summary>
    [ServiceContract(CallbackContract = typeof(IDirigentMasterContractCallback))]
    [ServiceKnownType(typeof(LaunchPlan))] // required to resolve IPlanRepo in PlanRepoMessage; see http://stackoverflow.com/questions/6108076/wcf-interface-return-type-and-knowntypes
    public interface IDirigentMasterContract
    {
        [OperationContract]
        void AddClient(string clientName);

        [OperationContract]
        void RemoveClient(string clientName);

        [OperationContract]
        void BroadcastMessage( Message msg );

        [OperationContract]
        IEnumerable<Message> ClientMessages( string clientName );
    }
}
