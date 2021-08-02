using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Dirigent
{
	[ProtoBuf.ProtoContract]
	[DataContract]
	public readonly struct AppIdTuple
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public readonly string MachineId;

		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public readonly string AppId;

		public AppIdTuple( string machineId, string appId )
		{
			MachineId = machineId;
			AppId = appId;
		}

		public AppIdTuple( string machineIdDotAppId )
		{
			AppIdTuple x = fromString( machineIdDotAppId, "" );
			MachineId = x.MachineId;
			AppId = x.AppId;
		}

		/// <summary>
		/// Parses string in form "machineId.appId". Uses defaultMachineId if not present in the string.
		/// </summary>
		/// <param name="machineIdDotAppId"></param>
		/// <param name="defaultMachineId"></param>
		/// <returns></returns>
		static public AppIdTuple fromString( string machineIdDotAppId, string defaultMachineId )
		{
			int dotIndex = machineIdDotAppId.IndexOf( '.' );
			if( dotIndex < 0 )
			{
				return new AppIdTuple(
						   defaultMachineId,
						   machineIdDotAppId
					   );
			}
			else
			{
				return new AppIdTuple(
						   machineIdDotAppId.Substring( 0, dotIndex ),
						   machineIdDotAppId.Substring( dotIndex + 1 )
					   );
			}
		}

		public override string ToString()
		{
			return MachineId + "." + AppId;
		}

		public string ToString( string? planName )
		{
			if( planName is null ) return ToString();
			else return ToString()+"@"+planName;
		}

		public override bool Equals( System.Object? obj )
		{
			// If parameter is null return false.
			if( obj == null )
				return false;

			// If parameter cannot be cast to AppIdTuple return false.
			if( !(obj is AppIdTuple) )
				return false;

			var p = (AppIdTuple) obj;

			// Return true if the fields match:
			return ( MachineId == p.MachineId ) && ( AppId == p.AppId );
		}

		public bool Equals( AppIdTuple p )
		{
			// Return true if the fields match:
			return ( MachineId == p.MachineId ) && ( AppId == p.AppId );
		}

		public override int GetHashCode()
		{
			return MachineId.GetHashCode() ^ AppId.GetHashCode();
		}


		public static bool operator ==( AppIdTuple t1, AppIdTuple t2 )
		{
			return t1.Equals( t2 );
		}

		public static bool operator !=( AppIdTuple t1, AppIdTuple person2 )
		{
			return !( t1.Equals( person2 ) );
		}

		public bool IsEmpty()
		{
			return string.IsNullOrEmpty(MachineId) && string.IsNullOrEmpty(AppId);
		}
	}
}
