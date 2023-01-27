using System;
using System.Collections.Generic;

namespace Dirigent
{

	public partial class ToolsRegistry
	{
		/// <summary>
		/// Starts a tool and monitor given temp file for changes.
		/// The file must already exists!
		/// If the file changes, notification is fired.
		/// If the tool terminates, the temp file is deleted.
		/// </summary>
		class ActionInstanceTempFileDecorator : IActionInstance
		{

			public bool Running => throw new NotImplementedException();

			IActionInstance _underlyingToolInst;
			Action? _onFileChanged;
			FileChangeMonitor? _fileChangeMonitor;
			string _fileName;

			public ActionInstanceTempFileDecorator( IActionInstance underlyingToolInst, string tempFileName, Action? onFileChanged )
			{
				_underlyingToolInst = underlyingToolInst;
				_onFileChanged = onFileChanged;
				_fileName = tempFileName;
			}

			public void Start()
			{
				if( _onFileChanged is not null )
					_fileChangeMonitor = new FileChangeMonitor( _fileName, _onFileChanged );

				_underlyingToolInst.Start();
			}

			public void Tick()
			{
				_underlyingToolInst.Tick();
			}

			public void Dispose()
			{
				_underlyingToolInst.Dispose();
				_fileChangeMonitor?.Dispose();
			}
		}
	}
}
