using System;
using System.Collections.Generic;
using System.Text;


namespace SyncableDict
{

	public class DemoValue
	{
		public string Field1;
		public string Field2;
	}

	// Holds what has changed in the original value since last reset.
	// This class can be serialized and sent to the remote endpoint over reliable connection.
	[ProtoBuf.ProtoContract]
	public class DemoChangeSet : IChangeSet<DemoValue>
	{
		public enum EField : ulong
		{
			Field1 = 1UL << 0,
			Field2 = 1UL << 1,

			_ALL_ = Field1 + Field2,
		}

		[ProtoBuf.ProtoMember(1)]
		public EField ChangeFlags;

		[ProtoBuf.ProtoMember(2)]
		public string Field1;

		[ProtoBuf.ProtoMember(3)]
		public string Field2;

		public bool HasChanges => ChangeFlags != 0;

		public void ResetChanges()
		{
			ChangeFlags = 0;
		}

		public void FromValue( DemoValue orig, DemoValue refer )
		{
			if( orig.Field1 != refer.Field1 )
			{
				ChangeFlags |= DemoChangeSet.EField.Field1;
				Field1 = orig.Field1;
				refer.Field1 = orig.Field1;
			}

			if( orig.Field2 != refer.Field2 )
			{
				ChangeFlags |= DemoChangeSet.EField.Field2;
				Field2 = orig.Field2;
				refer.Field2 = orig.Field2;
			}
		}


		public void ToValue( DemoValue orig )
		{
			if( ( ChangeFlags & DemoChangeSet.EField.Field1 ) != 0 )
			{
				orig.Field1 = Field1;
			}

			if( ( ChangeFlags & DemoChangeSet.EField.Field2 ) != 0 )
			{
				orig.Field2 = Field2;
			}
		}

	}


}
