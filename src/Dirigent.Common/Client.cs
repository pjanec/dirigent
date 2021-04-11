using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Net
{
    public class MasterConnectionTimeoutException : Exception
    {
        public string ip;
        public int port;

        public MasterConnectionTimeoutException(string ip, int port)
            : base(string.Format("Failed to connect to master {0}:{1}", ip, port))
        {
            this.ip = ip;
            this.port = port;
        }
    }

    public class UnknownClientName : Exception
    {
        public string name;

        public UnknownClientName(string name)
            : base(string.Format("Unknown client name '{0}'", name))
        {
            this.name =name;
        }
    }

    public class Client : IClient
    {
        public string Name { get { return _name;} }
		public string MasterIP { get { return _ipaddr; } }
		public int MasterPort { get { return _port; } }
		public string McastIP { get { return _mcastIP; } }
		public int McastPort { get { return _mcastPort; } }
		public string LocalIP { get { return _localIP; } }


        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private string _name;
        private string _ipaddr;
        private int _port;
        private string _mcastIP;
        private int _mcastPort;
        private string _localIP;
		private int _timeoutMs;


        private ProtoClient _protoClient;
        

        public Client( string name, string ipaddr, int port, string mcastIP, int mcastPort, string localIP, bool autoConn=false, int timeoutMs=5000 )
        {
            _name = name;
            _ipaddr = ipaddr;
            _port = port;
            _mcastIP = mcastIP;
            _mcastPort = mcastPort;
            _localIP = localIP;
			_timeoutMs = timeoutMs;

            _protoClient = new ProtoClient( ipaddr, port, autoConn );

            if( autoConn ) // if we want autoconnecting, do not wait for it...
            {
                _protoClient.ConnectAsync();
            }
            else // if we do not want autoconnecting, throw exception on disconnection
            {
                _protoClient.Disconnected = () =>
                {
                    throw new MasterConnectionTimeoutException( _ipaddr, _port );
                };

            }

        }
        
        public void Connect()
        {
            _protoClient.Connect();
        }

        public void Disconnect()
        {
            _protoClient.Disconnect();
        }

		static List<Message> _emptyMsgList = new List<Message>();
		
		private List<object> _messagesReceived = new List<object>();

		public IEnumerable<Message> ReadMessages()
        {
            _protoClient.GetMessages( ref _messagesReceived );
            
            // retype to Message (side effect: ignores mesages not derived from Message)
            return from x in _messagesReceived where x is Message select x as Message;
        }

        public void BroadcastMessage( Message msg )
        {
            msg.Sender = _name;
            _protoClient.SendMessage( msg );
        }

        public bool IsConnected()
        {
            return _protoClient.IsConnected;
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            _protoClient.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }



}
