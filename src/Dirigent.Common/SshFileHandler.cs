using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Manipulates file located on remote machines via SSH.
	/// Downloads file from remote machine to a local temp file.
	/// Uploads from temp file to remote machine.
	/// Deletes the temp file on Dispose.
	/// WARNING! the caller should make sure the tasks are cancelled before calling Dispose.
	/// </summary>
	public class SshFileHandler : Disposable
	{
		public string? LocalPath { get; protected set; }
		public string RemotePath { get; protected set; }

		SemaphoreSlim _busySemaphore = new( 1 ); // up to one operation at a time

		public SshFileHandler( string sshPath )
		{
			RemotePath = sshPath;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// delete the temp file
			if (LocalPath is not null)
			{
				try
				{
					File.Delete( LocalPath );
				}
				catch {}
				LocalPath = null;
			}

			// wait for current tasks to finish
			// the caller should make sure the tasks are cancelled before calling Dispose

			WaitForNotBusy( CancellationToken.None ).Wait();
		}
		
		async Task WaitForNotBusy( CancellationToken ct )
		{
			await _busySemaphore.WaitAsync( ct );
		}

		void ReleaseBusy()
		{
			_busySemaphore.Release();
		}

		public async Task DownloadAsync( CancellationToken ct )
		{
			await WaitForNotBusy( ct );

			try { await DoDownloadAsync(ct); }
			finally {ReleaseBusy();	}
		}

		public async Task UploadAsync( CancellationToken ct )
		{
			if( LocalPath is null )
			{
				throw new Exception( $"Cannot upload file, no local copy exists, download it first." );
			}

			await WaitForNotBusy( ct );

			try { await DoUploadAsync(ct); }
			finally { ReleaseBusy(); }
		}

		async Task DoDownloadAsync( CancellationToken ct )
		{
			// get temp file name, add extension from remote path
			var tempFileName = Path.GetTempFileName();

			var ext = System.IO.Path.GetExtension( RemotePath );
			if (!string.IsNullOrEmpty( ext ))
			{
				tempFileName += ext;
			}

			LocalPath = tempFileName;

			// simulate the download
			await Task.Delay( 1000, ct );
			File.WriteAllText( tempFileName, "Hello World!" );
		}

		async Task DoUploadAsync( CancellationToken ct )
		{
			// simulate the upload
			await Task.Delay( 1000, ct );
		}
		

	}
}
