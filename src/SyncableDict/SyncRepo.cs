using System;
using System.Collections.Generic;
using System.Text;

namespace SyncableDict
{
	public class SyncRepo<TKey, TValue, TChgSet>
		where TValue : new()
		where TChgSet : IChangeSet<TValue>, new()
	{
		[Flags]
		public enum EState
		{
			Dirty = 1 << 0, // value probably changed, needs to be compared with ref value and published
			Added = 1 << 1, // entry was recently added
			Removed = 1 << 2, // entry was removed
		}

		public struct Change
		{
			public EState State;  // updated (0, i.e. not added nor removed), added, removed
			public TKey Key;
			public TChgSet ChgSet;
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

		private List<TKey> _toRemove = new List<TKey>();
		private List<TKey> _toPublish = new List<TKey>();

		public enum EUpdateScanStrategy
		{
			CompareEach, // each element in the repo is checked with its reference value; allows changing value directly without telling the Dict that the value changed; slowest, good for small data sets only
			Marked, // all elemens are traversed and those marked Dirty are compared with reference value; requires calling Update or MarkUpdated; faster, but still needs to traverse all elements so it might be slow for big data sets
			Listed  // those elements recorded in the dirty list are compared with reference value; fastest for few changes in a large data set
		}

		public EUpdateScanStrategy UpdateScanStrategy = EUpdateScanStrategy.CompareEach;


		/// <summary>
		/// Adds the data item to the repository. Replaces if already exists.
		/// </summary>
		public void Add( TKey key, in TValue value, bool recordChanges=true )
		{
			var e = new Entry()
			{
				State = recordChanges ? EState.Added | EState.Dirty : 0,
				Curr = value,
				Ref = recordChanges ? new TValue() : value,
				ChgSet = new TChgSet()
			};

			_dataRepo[key] = e;

			if( recordChanges && UpdateScanStrategy == EUpdateScanStrategy.Listed )
				_toPublish.Add( key );
		}

		/// <summary>
		/// Removes stored data item in the repository
		/// </summary>
		public void Remove( in TKey key, bool recordChanges = true )
		{
			Entry e;
			if( _dataRepo.TryGetValue( key, out e ) )
			{
				if( recordChanges )
				{
					e.State |= EState.Removed | EState.Dirty;

					if( UpdateScanStrategy == EUpdateScanStrategy.Listed )
						_toPublish.Add( key );
				}
				else // remove right away
				{
					_dataRepo.Remove( key );
				}
			}

		}

		public void Update( in TKey key, TValue value, bool recordChanges=true )
		{
			Entry e;
			if( _dataRepo.TryGetValue( key, out e ) )
			{
				e.Curr = value;
				e.State |= EState.Dirty;

				if( recordChanges && UpdateScanStrategy == EUpdateScanStrategy.Listed )
					_toPublish.Add( key );
			}
		}

		public void MarkDirty(in TKey key)
		{
			Entry e;
			if (_dataRepo.TryGetValue(key, out e))
			{
				e.State |= EState.Dirty;
			}
		}

		public bool TryGetValue( in TKey key, out TValue value )
		{
			Entry e;
			if( _dataRepo.TryGetValue( key, out e ) )
			{
				if( (e.State & EState.Removed)==0 )
				{
					value = e.Curr;
					return true;
				}
			}
			value = default( TValue );
			return false;
		}

		private void EvalChange( KeyValuePair<TKey, Entry> kv, Action<Change> onChange )
		{
			var e = kv.Value;

			e.ChgSet.FromValue(e.Curr, e.Ref);

			var justAddedOrRemoved = e.State & (EState.Added | EState.Removed);

			if( justAddedOrRemoved != 0
					||
				e.ChgSet.HasChanges	// diff found
				 )
			{
				var chg = new Change()
				{
					State = justAddedOrRemoved,
					Key = kv.Key,
					ChgSet = e.ChgSet
				};

				onChange?.Invoke(chg);


				e.ChgSet.ResetChanges();

				if ((e.State & EState.Removed) > 0)
				{
					_toRemove.Add(kv.Key);
				}
			}

			// reset the flags to "unchanged" state
			e.State &= ~(EState.Added | EState.Dirty);
		}



		// Determinaes what have chnaged and invokes onChange delegate for each change found.
		// The change must be processed immediately within the delegate the change is reset when returned from the callback.
		// When this methos is finished, all changes are reset (can be used for clearing the changes)
		public void EvalChanges( Action<Change> onChange )
		{
			_toRemove.Clear();

			switch(UpdateScanStrategy)
			{
				case EUpdateScanStrategy.Listed:
					EvalListed(onChange);
					break;
				case EUpdateScanStrategy.Marked:
					EvalMarked(onChange);
					break;
				default:
					EvalAll(onChange);
					break;
			}

			// remove all marked to remove
			foreach ( var key in _toRemove )
			{
				_dataRepo.Remove( key );
			}
		}

		private void EvalAll(Action<Change> onChange)
		{
			foreach (var kv in _dataRepo)
			{
				EvalChange(kv, onChange);
			}
		}

		private void EvalMarked(Action<Change> onChange)
		{
			foreach (var kv in _dataRepo)
			{
				if( (kv.Value.State & EState.Dirty) != 0 )
				{
					EvalChange(kv, onChange);
				}
			}
		}

		private void EvalListed(Action<Change> onChange)
		{
			foreach (var k in _toPublish)
			{
				if (_dataRepo.TryGetValue(k, out var v))
				{
					EvalChange(new KeyValuePair<TKey, Entry>(k, v), onChange);
				}
			}
			_toPublish.Clear();
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
				case EState.Added:
				{
					var val = new TValue();
					chs.ToValue( val );
					Add( chg.Key, val );
					break;
				}

				case EState.Removed:
				{
					Remove( chg.Key );
					break;
				}

				default: // must be updated
				{
					Entry e;
					if( _dataRepo.TryGetValue( key, out e ) )
					{
						chs.ToValue( e.Curr );
					}
					break;
				}

			}
		}

	}
}
