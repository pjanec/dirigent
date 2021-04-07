using Dirigent.Common;
using System;
using System.Collections.Generic;

namespace Dirigent.Net
{
    public interface IClient : IDisposable
    {
        string Name { get; }
        string MasterIP { get; }    
        int MasterPort { get; }    
        string McastIP { get; }
        int McastPort { get; }
        string LocalIP { get; } // the adapter to bind to when mcasting
        void BroadcastMessage(Message msg);
        void Connect();
        void Disconnect();
        bool IsConnected();
        IEnumerable<Message> ReadMessages();
    }
}
