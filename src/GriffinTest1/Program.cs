using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GriffinTest1
{
	[ProtoContract]
	public class Authenticate
	{
	[ProtoMember(1)]
	public string UserName { get; set; }

	[ProtoMember(2)]
	public string Password { get; set; }
	}

	[ProtoContract]
	public class AuthenticateReply
	{
	[ProtoMember(1)]
	public bool Success { get; set; }

	[ProtoMember(2)]
	public string Decision { get; set; }
	}

	class ProtoBufSerializer : Griffin.Net.Protocols.Serializers.IMessageSerializer
	{
		static ConcurrentDictionary<string, Type> _types = new ConcurrentDictionary<string, Type>();

	    private static readonly string[] ContentTypes = {"application/protobuf"};

        public string[] SupportedContentTypes
        {
            get { return ContentTypes; }
        }

		public void Serialize(object source, Stream destination, out string contentType)
		{
			Serializer.NonGeneric.Serialize( destination, source);
			contentType = "application/protobuf;" + source.GetType().FullName;
		}

		public object Deserialize(string contentType, Stream source)
		{
			Type type;
			if (!_types.TryGetValue(contentType, out type))
			{
				int pos = contentType.IndexOf(";");
				if (pos == -1)
					throw new NotSupportedException("Expected protobuf");

				type = Type.GetType(contentType.Substring(pos + 1), true);
				_types[contentType] = type;
			}
            
			return Serializer.NonGeneric.Deserialize(type, source);
		}
	}

	class Program
	{
		static List<Griffin.Net.Channels.ITcpChannel> connectedClients = new List<Griffin.Net.Channels.ITcpChannel>();

		static void Main(string[] args)
		{
			Dirigent.Net.Message.RegisterProtobufTypeMaps();

			var settings = new Griffin.Net.ChannelTcpListenerConfiguration(
				() => new Griffin.Net.Protocols.MicroMsg.MicroMessageDecoder(new ProtoBufSerializer()),
				() => new Griffin.Net.Protocols.MicroMsg.MicroMessageEncoder(new ProtoBufSerializer())
				);

			var server = new Griffin.Net.ChannelTcpListener(settings);

			server.ClientConnected += (object sender, Griffin.Net.Protocols.ClientConnectedEventArgs x) =>
			{
				lock( connectedClients )
				{
					connectedClients.Add( x.Channel );
				}
			};
			server.ClientDisconnected += (object sender, Griffin.Net.Protocols.ClientDisconnectedEventArgs x) =>
			{
				lock( connectedClients )
				{
					connectedClients.Remove( x.Channel );
				}
			};

			server.MessageReceived = OnServerMessage;

			server.Start(System.Net.IPAddress.Any, 1234); 

			RunClient().Wait();
		}

		static void SendToAllConnectedClients( object msg )
		{
			lock( connectedClients )
			{
				foreach (var c in connectedClients)
				{
					c.Send( msg );
				}
			}
		}

		private static void OnServerMessage(Griffin.Net.Channels.ITcpChannel channel, object message)
		{
			var auth = (Authenticate) message as Authenticate;
			if( auth != null )
			{
				var reply = new AuthenticateReply() {Success = true};
				channel.Send( reply );
			}
			else
			if( message.GetType() == typeof(Dirigent.Net.AppsStateMessage) )
			{
				// broadcast back to all
				SendToAllConnectedClients( message );
			}

		}

		private static async Task RunClient()
		{
			var client = new Griffin.Net.ChannelTcpClient(
				new Griffin.Net.Protocols.MicroMsg.MicroMessageEncoder(new ProtoBufSerializer()),
				new Griffin.Net.Protocols.MicroMsg.MicroMessageDecoder(new ProtoBufSerializer())
				);

			await client.ConnectAsync(System.Net.IPAddress.Parse("127.0.0.1"), 1234);

			var appStateDict = new Dictionary<Dirigent.Common.AppIdTuple, Dirigent.Common.AppState>();
			appStateDict[new Dirigent.Common.AppIdTuple("m1.a1")] = new Dirigent.Common.AppState() { Started=true, Running = true, PlanName="p1" };
			var msg = new Dirigent.Net.AppsStateMessage( appStateDict );
			await client.SendAsync( msg );
			object received = await client.ReceiveAsync();
			var appStateMsg = received as Dirigent.Net.AppsStateMessage;
			if( appStateMsg != null )
			{
			}


			for (int i = 0; i < 20000; i++)
			{
				await client.SendAsync(new Authenticate { UserName = "jonas", Password = "king123" });
				
				var reply = (AuthenticateReply)await client.ReceiveAsync();

				if (reply.Success)
				{
					//Console.WriteLine("Client: Yay, we are logged in.");
				}
				else
				{
					//Console.WriteLine("Client: " + reply.Decision);
				}
			}

			await client.CloseAsync();
		}
	}
}
