using System;
namespace Dirigent.Net
{
    public interface IClient
    {
        string Name { get; }
        void BroadcastMessage(Message msg);
        void Connect();
        void Disconnect();
        System.Collections.Generic.IEnumerable<Message> ReadMessages();
    }
}
