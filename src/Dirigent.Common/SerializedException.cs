using System;
using System.Collections.Generic;

namespace Dirigent
{
	public class DeserializedException : Exception
	{
		string _message;
		string? _stackTrace;
		
		public override string Message => _message;
		public override string? StackTrace => _stackTrace;

		public DeserializedException()
		{
			_message = "";
			_stackTrace = null;
		}

		public DeserializedException( string message, string? stackTrace )
		{
			_message = message;
			_stackTrace = stackTrace;
		}
	}

	[MessagePack.MessagePackObject]
	public class SerializedException
	{
		[MessagePack.Key( 1 )]
		public string TypeName = "";

		[MessagePack.Key( 2 )]
		public string Message = "";

		[MessagePack.Key( 3 )]
		public string? StackTrace;

		[MessagePack.Key( 4 )]
		public SerializedException? InnerException;

		
		public SerializedException() {}
		
		public SerializedException( Exception ex )
		{
			TypeName = ex.GetType().FullName!;
			Message = ex.Message;
			StackTrace = ex.StackTrace;
			if (ex.InnerException != null)
				InnerException = new SerializedException( ex.InnerException );
		}

		public static List<SerializedException> MkList( IEnumerable<Exception> exs )
		{
			var res = new List<SerializedException>();
			foreach (var ex in exs)
				res.Add( new SerializedException( ex ) );
			return res;
		}

		public Exception ToException()
		{
			var e = new DeserializedException( Message, StackTrace );
			return e;
		}
	}
}
