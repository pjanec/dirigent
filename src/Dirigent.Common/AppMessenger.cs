using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Common
{
	public class AppMessenger
	{
		// singleton API
		static AppMessenger _instance = null;
		static public AppMessenger Instance
		{
			get
			{
				if( _instance == null )
				{
					_instance = new AppMessenger();
				}
				return _instance;
			}
		}

		Dictionary<System.Type, List<object>> listeners = new Dictionary<Type, List<object>>();

		public void Send<T>( T message )
		{
			// send to all recipients registered to this type of message

			List<object> list;
			
			if( !listeners.TryGetValue(typeof(T), out list )	)
				return;
			
			foreach( var a in list )
			{
				var typeA = a as Action<T>;
				if( typeA != null )
				{
					typeA( message );
				}
			}
		}

		public void Register<T>( Action<T> action )
		{
			List<object> list;
			
			if( !listeners.TryGetValue(typeof(T), out list )	)
			{
				list = new List<object>();
				listeners[typeof(T)] = list;
			}

			list.Add( action );
		}

		public void Unregister<T>( Action<T> action )
		{
			List<object> list;
			
			if( !listeners.TryGetValue(typeof(T), out list )	)
			{
				return;
			}
			list.Remove( action );
		}

		public void Dispose()
		{
			listeners.Clear();
		}


	}
}
