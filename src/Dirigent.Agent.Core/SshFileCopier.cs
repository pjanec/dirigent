using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace Dirigent
{
	/// <summary>
	/// Downloads file from remote machine to a local file.
	/// Uploads from local file to remote machine.
	/// </summary>
	public class SshFileCopier : Disposable
	{
		SftpClient _sftp;
		string _hostAddr;

		public SshFileCopier( string host, int port, string user, string password  )
		{
			_hostAddr = $"{user}@{host}:{port}";
			_sftp = new SftpClient( host, port, user, password );
			_sftp.Connect();
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;
			_sftp.Disconnect();
			_sftp.Dispose();
		}

		/// <param name="localPath">C:\mydir\myfile.txt</param>
		/// <param name="remotePath">/C:/mydir/myfile.txt</param>
		public async Task DownloadAsync( string localPath, string remotePath )
		{
			try
			{
				using (var fs = new FileStream( localPath, FileMode.Create ))
				{
					// download file to local stream
					var asyncResult = _sftp.BeginDownloadFile( remotePath, fs );

					// await it
					await Task.Factory.FromAsync( asyncResult, _sftp.EndDownloadFile ); // complains about 
				}
			}
			catch( Exception ex )
			{
				throw new Exception( $"Failed to download file '{remotePath}' from {_hostAddr}: {ex.Message}");
			}
		}

		/// <param name="localPath">C:\mydir\myfile.txt</param>
		/// <param name="remotePath">/C:/mydir/myfile.txt</param>
		public async Task UploadAsync( string localPath, string remotePath  )
		{
			try
			{
				using (var fs = new FileStream( localPath, FileMode.Open ))
				{
					// download file to local stream
					var asyncResult = _sftp.BeginUploadFile( fs, remotePath );

					// await it
					await Task.Factory.FromAsync( asyncResult, _sftp.EndUploadFile );
				}
			}
			catch( Exception ex )
			{
				throw new Exception( $"Failed to upload file '{remotePath}' to {_hostAddr}: {ex.Message}");
			}
		}

	}
}
