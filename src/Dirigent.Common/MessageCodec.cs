using System;
using System.Collections.Generic;
using System.Text;

namespace Dirigent.Net
{
	public class MessageCodec
	{
		public struct Header
		{
			public uint DataSize;
		}
		public Action<Header, byte[], long, long>? MessageReceived;

		// keeps adding message segments until full length is there, then we copy the message out
		// and remove it from the beginning of buffer, moving the remaining  data to the beginning
		NetCoreServer.Buffer _awBlockBuf = new NetCoreServer.Buffer();
		long _awBlockLen = 0; // the size of the awaited block
		Action<byte[], long, long>? _onBlockReceived;
		Header _header; // the header recently received


		public const long HeaderLen = 4;

		public MessageCodec()
		{
			AwaitHeader();
		}

		void GotHeader( byte[] data, long offset, long size )
		{
			_header = new Header();

			unsafe { fixed( byte *p = &data[offset] )
			{
				_header.DataSize = *( ( uint * )( p + 0 ) );
			}
				   }

			AwaitBody( _header.DataSize );
		}

		void GotBody( byte[] data, long offset, long size )
		{
			MessageReceived?.Invoke( _header, data, offset, size );

			AwaitHeader();
		}


		void AwaitHeader()
		{
			_awBlockLen = HeaderLen;
			_onBlockReceived = GotHeader;
		}

		void AwaitBody( long size )
		{
			_awBlockLen = size;
			_onBlockReceived = GotBody;
		}

		public void ReceivedMessagePart( byte[] buffer, long offset, long size )
		{
			_awBlockBuf.Append( buffer, offset, size );

			while( _awBlockBuf.Size - _awBlockBuf.Offset >= _awBlockLen )
			{
				long currBlockLen = _awBlockLen;

				_onBlockReceived?.Invoke( _awBlockBuf.Data, _awBlockBuf.Offset, _awBlockLen ); // note: changes the _awBlockLen

				_awBlockBuf.Shift( currBlockLen );
			}
			// remove the already used part of the buffer
			_awBlockBuf.Remove( 0, _awBlockBuf.Offset );
		}

		// writes the header to the buffer at given offset
		// returns numbers of bytes written
		static public void ConstructHeader( in Header hdr, byte[] buf, long offs )
		{
			unsafe { fixed( byte *p = &buf[offs] )
			{
				*( ( uint * )( p + 0 ) ) = hdr.DataSize;
			}
		   }
		}

		static public void ConstructHeader( in Header hdr, System.IO.MemoryStream stream )
		{
			byte[] buf = new byte[MessageCodec.HeaderLen];
			ConstructHeader( hdr, buf, 0L );
			stream.Write( buf, 0, ( int ) MessageCodec.HeaderLen );
		}

		public void ConstructMessage( System.IO.MemoryStream stream, Header hdr, byte[] data, long offset, long size )
		{
			ConstructHeader( hdr, stream );
			stream.Write( data, ( int )offset, ( int )size );
		}

	}
}
