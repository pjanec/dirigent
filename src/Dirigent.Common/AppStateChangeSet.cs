using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Dirigent
{
	public class AppStateChangeSet : SyncableDict.IChangeSet<AppState>
	{
		public enum EField : ulong
		{
			Name		= 1UL << 0,
			Value		= 1UL << 1,
		}

		public EField ChangeFlags;
		//public string Name;
		//public string Value;

		public bool HasChanges => ChangeFlags != 0;

		public void ResetChanges()
		{
			ChangeFlags = 0;
		}

		public void FromValue( AppState orig, AppState refer )
		{
			//if (orig.Name != refer.Name)
			//{
			//	ChangeFlags |= DemoChangeSet.EField.Name;
			//	Name = orig.Name;
			//	refer.Name = orig.Name;
			//}

			//if (orig.Value != refer.Value)
			//{
			//	ChangeFlags |= DemoChangeSet.EField.Value;
			//	Value = orig.Value;
			//	refer.Value = orig.Value;
			//}
		}


		public void ToValue( AppState orig )
		{
			//if ((ChangeFlags & DemoChangeSet.EField.Name) != 0)
			//{
			//	orig.Name = Name;
			//}

			//if ((ChangeFlags & DemoChangeSet.EField.Value) != 0)
			//{
			//	orig.Value = Value;
			//}
		}

	}


}
