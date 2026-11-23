using System;
using System.Collections.Generic;
using System.Text;

namespace Dirigent.Net
{
	public class MsgPackCodec
	{
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

		public Action<Net.Message>? MessageReceived;

		/// <summary>
		/// Called (on the receiving thread) when a message body could not be decoded - garbage, or a peer
		/// that speaks an older encoding. The message is dropped either way.
		/// </summary>
		public Action<Exception>? DecodeFailed;

		/// <summary>Where the messages taken off the wire are counted; null = not counted.</summary>
		public TrafficStats? Traffic;

		private MessageCodec _msgCodec = new MessageCodec();

		/// <summary>
		/// What goes on the wire: every class carries integer keys (no field names), and a body above
		/// a few dozen bytes is LZ4-compressed. Not contractless - a member without [Key] must fail
		/// the key coverage test, not vanish from the wire.
		/// </summary>
		public static readonly MessagePack.MessagePackSerializerOptions Options =
			MessagePack.MessagePackSerializerOptions.Standard.WithCompression( MessagePack.MessagePackCompression.Lz4BlockArray );

		static MsgPackCodec()
		{
			MessagePack.MessagePackSerializer.DefaultOptions = Options;
		}

		public MsgPackCodec()
		{
			_msgCodec.MessageReceived = OnMsgReceived;
		}

		public void Reset()
		{
			_msgCodec.Reset();
		}
		
		void OnMsgReceived( MessageCodec.Header hdr, byte[] data, long offset, long size )
		{
			var instance = Deserialize<Net.Message>( data, offset, size, out var error );
			if( error is not null )
			{
				if( DecodeFailed is null ) log.Error( $"Deserialize<{typeof( Net.Message ).FullName}> failed: {error.Message} {error.InnerException}" );
				else DecodeFailed( error );
				return;
			}
			if( instance == null ) return;
			Traffic?.Received( instance.GetType(), MessageCodec.HeaderLen + size );
			MessageReceived?.Invoke( instance );
		}

		static object _globalLock = new object();


		public static void Serialize<T>( System.IO.MemoryStream stream, T instance )
		{
			var outBodyStream = new System.IO.MemoryStream( 1000 );
			lock (_globalLock)
			{
				MessagePack.MessagePackSerializer.Serialize<T>( outBodyStream, instance, Options );
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
			var instance = Deserialize<T>( body, offset, size, out var error );
			if( error is not null )
				log.Error( $"Deserialize<{typeof( T ).FullName}> failed: {error.Message} {error.InnerException}" );
			return instance;
		}

		/// <summary>Decodes a body; the reason goes to <paramref name="error"/> instead of the log.</summary>
		public static T? Deserialize<T>( byte[] body, long offset, long size, out Exception? error )
		{
			error = null;
			var inBodyStream = new System.IO.MemoryStream( body, ( int ) offset, ( int ) size );
			try
			{
				T? instance;
				lock (_globalLock)
				{
					instance = MessagePack.MessagePackSerializer.Deserialize<T>( inBodyStream, Options );
				}
				return instance;
			}
			catch (Exception e)
			{
				error = e;
				return default(T);
			}
		}

		public void ReceivedMessagePart( byte[] buffer, long offset, long size )
		{
			_msgCodec.ReceivedMessagePart( buffer, offset, size );
		}
	}

}
