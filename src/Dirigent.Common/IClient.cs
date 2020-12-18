using System;
namespace Dirigent.Net
{
    public interface IClient : IDisposable
    {
        string Name { get; }
        string MasterIP { get; }    
        int MasterPort { get; }    
        void BroadcastMessage(Message msg);
        void Connect();
        void Disconnect();
        bool IsConnected();
        System.Collections.Generic.IEnumerable<Message> ReadMessages();
    }
}
