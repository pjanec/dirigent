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
		private System.IO.MemoryStream _outBodyStream = new System.IO.MemoryStream( 1000 );

		public MsgPackCodec()
		{
			_msgCodec.MessageReceived = OnMsgReceived;
		}

		void OnMsgReceived( MessageCodec.Header hdr, byte[] data, long offset, long size )
		{
			var instance = Deserialize<Net.Message>( data, offset, size );
			if( instance == null ) return;
			MessageReceived?.Invoke( instance );
		}


		public void Serialize<T>( System.IO.MemoryStream stream, in T instance )
		{
			_outBodyStream.SetLength( 0 );
			MessagePack.MessagePackSerializer.Serialize<T>( _outBodyStream, instance );

			{	
				var data = _outBodyStream.GetBuffer();
				var len = _outBodyStream.Position;
			}
			

			var hdr = new MessageCodec.Header()
			{
				DataSize = ( uint ) _outBodyStream.Position
			};

			_msgCodec.ConstructMessage(
				stream,
				hdr,
				_outBodyStream.GetBuffer(),
				0L,
				_outBodyStream.Position
			);
		}

		public T? Deserialize<T>( byte[] body, long offset, long size )
		{
			var inBodyStream = new System.IO.MemoryStream( body, ( int ) offset, ( int ) size );
			try
			{
				T instance = MessagePack.MessagePackSerializer.Deserialize<T>( inBodyStream );
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
