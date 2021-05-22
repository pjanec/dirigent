/*
using System;

namespace Dirigent
{
	[ProtoBuf.ProtoContract]
	public struct AppDefRec
	{
		[ProtoBuf.ProtoMember( 1 )]
		public AppDef AppDef;

		/// <summary>
		/// What plan the app def comes from. null/empty = none	(it's a default app)
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public string? PlanName;

		public AppDefRec( AppDef appDef, string? planName )
		{
			AppDef = appDef;
			PlanName = planName;
		}

		public override int GetHashCode()
		{
			return this.AppDef.GetHashCode() ^ this.PlanName?.GetHashCode() ?? 0;
		}

		public bool Equals( AppDefRec other )
		{
			if(
				this.AppDef == other.AppDef &&
				this.PlanName == other.PlanName
			)
				return true;
			else
				return false;
		}

		public override bool Equals( Object? obj )
		{
			if( obj == null )
				return false;

			var typed = (AppDefRec) obj;
			return Equals( typed );
		}

		public static bool operator ==( AppDefRec t1, AppDefRec t2 )
		{
			return t1.Equals( t2 );
		}

		public static bool operator !=( AppDefRec t1, AppDefRec t2 )
		{
			return !( t1.Equals( t2 ) );
		}

		public override string ToString()
		{
			if( !string.IsNullOrEmpty(PlanName) )
				return $"{AppDef.Id}";
			else
				return $"{AppDef.Id}@{PlanName}";
		}
	}
}
*/
