using System;
using System.Collections.Generic;
using System.Text;

namespace Dirigent.Net
{
	public class MsgPackCodec
	{
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

		public Action<Net.Message>? MessageReceived;

		private MessageCodec _msgCodec = new MessageCodec();

		public MsgPackCodec()
		{
			_msgCodec.MessageReceived = OnMsgReceived;
			MessagePack.MessagePackSerializer.DefaultOptions = MessagePack.Resolvers.ContractlessStandardResolver.Options;
		}

		void OnMsgReceived( MessageCodec.Header hdr, byte[] data, long offset, long size )
		{
			var instance = Deserialize<Net.Message>( data, offset, size );
			if( instance == null ) return;
			MessageReceived?.Invoke( instance );
		}

		static object _globalLock = new object();


		public static void Serialize<T>( System.IO.MemoryStream stream, T instance )
		{
			var outBodyStream = new System.IO.MemoryStream( 1000 );
			lock (_globalLock)
			{
				MessagePack.MessagePackSerializer.Serialize<T>( outBodyStream, instance );
			}

			var hdr = new MessageCodec.Header()
			{
				DataSize = ( uint ) outBodyStream.Position
			};

			MessageCodec.ConstructMessage(
				stream,
				hdr,
				outBodyStream.GetBuffer(),
				0L,
				outBodyStream.Position
			);
		}

		public static T? Deserialize<T>( byte[] body, long offset, long size )
		{
			var inBodyStream = new System.IO.MemoryStream( body, ( int ) offset, ( int ) size );
			try
			{
				T? instance;
				lock (_globalLock)
				{
					instance = MessagePack.MessagePackSerializer.Deserialize<T>( inBodyStream );
				}
				return instance;
			}
			catch (Exception e)
			{
				log.Error( $"Deserialize<{typeof( T ).FullName}> failed: {e.Message} {e.InnerException}" );
				return default(T);
			}
		}

		public void ReceivedMessagePart( byte[] buffer, long offset, long size )
		{
			_msgCodec.ReceivedMessagePart( buffer, offset, size );
		}
	}

}
