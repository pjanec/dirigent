using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent.Scripts
{
	public class DownloadFileZippedController : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public override void Init()
		{
			log.Info($"Init with args: '{Args}'");
			Coroutine = new Coroutine( Run() );
			StatusText = "Initialized";
		}

		public override void Done()
		{
			log.Info("Done!");

			StatusText = "Finished";
		}

		System.Collections.IEnumerable Run()
		{
			log.Info("Run!");
			yield return null;
		}
	}

	public class DownloadFileZippedWorker : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public override void Init()
		{
			log.Info($"Init with args: '{Args}'");
			Coroutine = new Coroutine( Run() );
			StatusText = "Initialized";
		}

		public override void Done()
		{
			log.Info("Done!");

			StatusText = "Finished";
		}

		System.Collections.IEnumerable Run()
		{
			log.Info("Run!");
			yield return null;
		}
	}
}
