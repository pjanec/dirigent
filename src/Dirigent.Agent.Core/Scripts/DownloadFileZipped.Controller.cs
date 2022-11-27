using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent.Scripts.DownloadFileZipped
{
	public class Controller : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		protected override Task Init()
		{
			log.Info($"Init with args: '{Args}'");
			StatusText = "Initialized";
			return Task.CompletedTask;
		}

		protected override void Done()
		{
			log.Info("Done!");

			StatusText = "Finished";
		}

		protected async override Task Run( CancellationToken ct )
		{
			log.Info("Run!");
			await WaitUntilCancelled(ct);
		}
	}

}
