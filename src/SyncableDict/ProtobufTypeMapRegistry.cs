using System;
using System.Collections.Generic;


namespace SyncableDict
{
	public class ProtoTypeMap
	{
		private uint _dynamicTypeBase = 1000;

		public Dictionary<uint, System.Type> TypeMap = new Dictionary<uint, Type>();

		// returns the id to be used when sending a message of that type
		public uint RegisterDynamicType( Type t )
		{
			foreach (var kv in TypeMap)
			{
				if (kv.Value == t)
				{
					return kv.Key;
				}
			}
			var ret = _dynamicTypeBase;
			TypeMap[_dynamicTypeBase] = t;
			_dynamicTypeBase++;
			return ret;
		}



	}


}
