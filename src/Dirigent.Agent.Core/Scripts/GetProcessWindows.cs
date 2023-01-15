#if Windows
using System;
using System.Linq;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Dirigent;
using System.Diagnostics;

namespace Dirigent.Scripts.BuiltIn
{

	public class GetProcessWindows : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/GetProcessWindows.cs";

		public class TArgs
		{
			public int PID;
		};

		
		public class WinInfo
		{
			public long Handle;
			public string Title = "";
			public long Style;
		}

		public class TResult
		{
			public List<WinInfo> Windows = new List<WinInfo>();
		}

		protected override Task<byte[]?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args );
			if( args is null ) throw new NullReferenceException("Args is null");
			if( args.PID == 0 ) throw new NullReferenceException("Args.PID is 0");

			var result = new TResult();
			foreach( var x in  WinApi.GetProcessWindows( args.PID ) )
			{
				var wi = new WinInfo()
				{
					Handle = x.Handle.ToInt64(),
					Title = x.Title,
					Style = WinApi.GetWindowLong( x.Handle, WinApi.GWL_STYLE )
				};
				result.Windows.Add( wi );

			};

			return Task.FromResult<byte[]?>( Tools.Serialize(result) );
		}

	}

}
#endif