using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Dirigent.Net;

namespace Dirigent
{
    /// <summary>
    /// Interface abstracting the network server from the Master's perspective,
    /// allowing a mock server to be injected for testing.
    /// </summary>
    public interface IMasterServer : IDisposable
    {
        void SendToSingle(Message msg, string clientName);
        void SendToAllSubscribed(Message msg, EMsgRecipCateg msgCategoryMask);
        void BufferMessageReceived(Message msg);

        void Tick(Action<Message>? act = null);
        IEnumerable<ClientIdent> Clients { get; }
        Socket? GetClientSocket(string clientName);
        bool IsDisposed { get; }
    }
}


