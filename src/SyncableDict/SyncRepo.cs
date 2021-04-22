using System;
using System.Collections.Generic;
using System.Text;

namespace SyncableDict
{
	public class SyncRepo<TKey, TValue, TChgSet>
		where TValue : new()
		where TChgSet : IChangeSet<TValue>, new()
	{
		public enum EState
		{
			Created,
			Updated,
			Destroyed
		}

		private class Entry
		{
			public EState State;
			public TValue Curr;
			public TValue Ref;
			public TChgSet ChgSet;
		}

		private Dictionary<TKey, Entry> _dataRepo = new Dictionary<TKey, Entry>();
		//private Dictionary<TKey, TChgSet> _additions = new Dictionary<TKey, TChgSet>();
		//private Dictionary<TKey, TChgSet> _removals = new Dictionary<TKey, TChgSet>();

		/// <summary>
		/// Adds the data item to the repository. Replaces if already exists.
		/// </summary>
		public void Add( TKey key, in TValue value )
		{
			var e = new Entry()
			{
				State = EState.Created,
				Curr = value,
				Ref = new TValue(),
				ChgSet = new TChgSet()
			};

			_dataRepo[key] = e;
		}

		/// <summary>
		/// Removes stored data item in the repository
		/// </summary>
		public void Remove( in TKey key )
		{
			Entry e;
			if( _dataRepo.TryGetValue( key, out e ) )
			{
				e.State = EState.Destroyed;
			}

		}

		public void Update( in TKey key, TValue value )
		{
			Entry e;
			if( _dataRepo.TryGetValue( key, out e ) )
			{
				e.Curr = value;
			}

		}

		public bool TryGetValue( in TKey key, out TValue value )
		{
			Entry e;
			if( _dataRepo.TryGetValue( key, out e ) )
			{
				if( e.State != EState.Destroyed )
				{
					value = e.Curr;
					return true;
				}
			}
			value = default( TValue );
			return false;
		}

		private	List<TKey> _toRemove = new List<TKey>();

		public struct Change
		{
			public EState State;
			public TKey Key;
			public TChgSet ChgSet;
		}

		// Determinaes what have chnaged and invokes onChange delegate for each change found.
		// The change must be processed immediately within the delegate the change is reset when returned from the callback.
		// When this methos is finished, all changes are reset (can be used for clearing the changes)
		public void EvalChanges( Action<Change> onChange )
		{
			_toRemove.Clear();

			foreach( var kv in _dataRepo )
			{
				var e = kv.Value;

				e.ChgSet.FromValue( e.Curr, e.Ref );

				var chg = new Change()
				{
					State = e.State,
					Key = kv.Key,
					ChgSet = e.ChgSet
				};

				onChange?.Invoke( chg );


				e.ChgSet.ResetChanges();

				switch( e.State )
				{
					case EState.Created:
						e.State = EState.Updated;
						break;
					case EState.Destroyed:
						_toRemove.Add( kv.Key );
						break;
				}
			}

			foreach( var key in _toRemove )
			{
				_dataRepo.Remove( key );
			}
		}


		public void ResetChanges()
		{
			EvalChanges( null );
		}


		public void ApplyChange( Change chg )
		{
			var key = chg.Key;
			var chs = chg.ChgSet;

			switch( chg.State )
			{
				case EState.Created:
				{
					var val = new TValue();
					chs.ToValue( val );
					Add( chg.Key, val );
					break;
				}

				case EState.Updated:
				{
					Entry e;
					if( _dataRepo.TryGetValue( key, out e ) )
					{
						chs.ToValue( e.Curr );
					}
					break;
				}

				case EState.Destroyed:
				{
					Remove( chg.Key );
					break;
				}
			}
		}

	}
}
