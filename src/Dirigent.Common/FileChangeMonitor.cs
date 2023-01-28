
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;
using System.IO;
using System.Xml.Linq;
using System.Threading;
using System.IO.Enumeration;

namespace Dirigent
{
	// Fires the action when file changes.
	// Calls the onChange from own task/thread, make sure your handler is thread safe!
	public class FileChangeMonitor : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		public string FileName;
		public int CheckIntervalMs { get; protected set; }

		Func<CancellationToken, Task> _onFileChanged;
		DateTime _lastModificationTime = DateTime.MinValue;

		Task? _periodicCheckTask;
		CancellationTokenSource _cts = new CancellationTokenSource();

		public FileChangeMonitor( string fileName, Func<CancellationToken, Task> onFileChanged, int checkIntervalMs = 500 )
		{
			FileName = fileName;
			_onFileChanged = onFileChanged;
			CheckIntervalMs = checkIntervalMs;

			// remember file's last mode time
			_lastModificationTime = GetModTime();

			// start monitoring the file...
			_periodicCheckTask = Task.Run( () => PeriodicCheckingTask( _cts.Token ) );
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// TODO: stop monitoring the file...
			if( _periodicCheckTask != null )
			{
				_cts.Cancel();
				_periodicCheckTask.Wait();
				_periodicCheckTask = null;
			}
		}

		DateTime GetModTime()
		{
			try {
				return File.GetLastWriteTimeUtc( FileName );
			} catch {
				return DateTime.MinValue;
			}
		}

		// Forces the check of the file change outside of normal periodic interval, fires the notification if so
		// Use this if you want to check the file for changes immediately.
		public Task ForceCheckAsync( CancellationToken ct )
		{
			return CheckAsync( ct );
		}

		async Task CheckAsync( CancellationToken ct )
		{
			// get file modification time
			var modifTime = GetModTime();

			if( modifTime == DateTime.MinValue ) // failed to get the time
			{
				return;
			}

			if ( modifTime > _lastModificationTime )
			{
				// file changed
				_lastModificationTime = modifTime;

				// notify
				Console.WriteLine( $"File {FileName} changed, notifying..." );
				await _onFileChanged( ct );
				return;
			}

			// file replaced with an older one?
			if( modifTime < _lastModificationTime )
			{
				_lastModificationTime = modifTime;
			}

			return;
		}

		async Task PeriodicCheckingTask( CancellationToken ct )
		{
			while( !ct.IsCancellationRequested )
			{
				try
				{
					await Task.Delay( CheckIntervalMs, ct );
					await CheckAsync( ct );
				}
				catch( TaskCanceledException ) { break; }
			}
		}
	}
}

