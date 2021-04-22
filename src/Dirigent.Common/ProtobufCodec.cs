using System;
using System.Collections.Generic;
using System.Text;

namespace Dirigent.Net
{
	public class ProtoBufCodec
	{
		public Action<uint, object>? MessageReceived;

		private MessageCodec _msgCodec = new MessageCodec();
		private System.IO.MemoryStream _outBodyStream = new System.IO.MemoryStream( 1000 );
		private Dictionary<uint, System.Type> _typeMap;

		public ProtoBufCodec( Dictionary<uint, System.Type> typeMap )
		{
			_typeMap = typeMap;
			_msgCodec.MessageReceived = OnMsgReceived;
		}

		System.Type? MsgCodeToType( uint msgCode )
		{
			System.Type? t;
			if( _typeMap.TryGetValue( msgCode, out t ) )
			{
				return t;
			}
			return null;
		}

		public uint TypeToMsgCode( System.Type? t )
		{
			foreach( var kv in _typeMap )
			{
				if( kv.Value == t )
					return kv.Key;
			}
			return uint.MaxValue;
		}


		void OnMsgReceived( MessageCodec.Header hdr, byte[] data, long offset, long size )
		{
			var t = MsgCodeToType( hdr.MsgCode );
			if( t != null )
			{
				var instance = GetProtoMessage( t, data, offset, size );
				MessageReceived?.Invoke( hdr.MsgCode, instance );
			}
		}


		public void ConstructProtoMessage<T>( System.IO.MemoryStream stream, uint msgCode, in T instance )
		{
			_outBodyStream.SetLength( 0 );
			ProtoBuf.Serializer.Serialize<T>( _outBodyStream, instance );

			var hdr = new MessageCodec.Header()
			{
				MsgCode = msgCode,
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

		public void ConstructProtoMessage<T>( System.IO.MemoryStream stream, T instance )
		{
			if( instance is null ) return;
			uint msgCode = TypeToMsgCode( instance.GetType() );
			if( msgCode == uint.MaxValue ) throw new System.ArgumentException( $"type '{instance.GetType().FullName}' not registered in type map" );
			ConstructProtoMessage( stream, msgCode, instance );
		}

		public object GetProtoMessage( System.Type t, byte[] body, long offset, long size )
		{
			var inBodyStream = new System.IO.MemoryStream( body, ( int ) offset, ( int ) size );
			object instance = ProtoBuf.Serializer.Deserialize( t, inBodyStream );
			return instance;
		}

		public T GetProtoMessage<T>( MessageCodec.Header hdr, byte[] body, long offset, long size )
		{
			var inBodyStream = new System.IO.MemoryStream( body, ( int ) offset, ( int ) size );
			T instance = ProtoBuf.Serializer.Deserialize<T>( inBodyStream );
			return instance;
		}

		public void ReceivedMessagePart( byte[] buffer, long offset, long size )
		{
			_msgCodec.ReceivedMessagePart( buffer, offset, size );
		}
	}

}
