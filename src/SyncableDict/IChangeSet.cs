using System;
using System.Collections.Generic;
using System.Text;

namespace SyncableDict
{
	public interface IChangeSet<TValue>
	{
		// Resets the change set to "no chnages"
		void ResetChanges();

		// Some change recorded?
		bool HasChanges { get; }

		// Compares original with the reference, records changes to the change set, updates the reference to match the original
		void FromValue( TValue original, TValue reference );

		// Writes the changes from the change set to the original value
		void ToValue( TValue original );

	}
}
