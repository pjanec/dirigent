using System;
using System.Collections.Generic;

namespace Dirigent
{

	// https://stackoverflow.com/questions/3928822/comparing-2-dictionarystring-string-instances
	public static class DictionaryExtensions
	{
		public static bool DictionaryEqual<TKey, TValue>(
			this IDictionary<TKey, TValue> first, IDictionary<TKey, TValue>? second )
		{
			return first.DictionaryEqual( second, null );
		}

		public static bool DictionaryEqual<TKey, TValue>(
			this IDictionary<TKey, TValue> first, IDictionary<TKey, TValue>? second,
			IEqualityComparer<TValue>? valueComparer )
		{
			return DictionariesEqual( first, second, valueComparer );
		}

		public static bool DictionariesEqual<TKey, TValue>(
			IDictionary<TKey, TValue>? first, IDictionary<TKey, TValue>? second,
			IEqualityComparer<TValue>? valueComparer )
		{
			if( first == second ) return true;
			if( ( first == null ) || ( second == null ) ) return false;
			if( first.Count != second.Count ) return false;

			valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;

			foreach( var kvp in first )
			{
				TValue? secondValue;
				if( !second.TryGetValue( kvp.Key, out secondValue ) ) return false;
				if( !valueComparer.Equals( kvp.Value, secondValue ) ) return false;
			}
			return true;
		}
	}
}
