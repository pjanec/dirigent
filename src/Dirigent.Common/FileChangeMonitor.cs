
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
	// fires the action when file changes
	public class FileChangeMonitor : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		public string FileName;
			
		public FileChangeMonitor( string fileName, Action onFileChanged )
		{
			FileName = fileName;
			// TODO: remember file's last mode time
			// start monitoring the file...
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;
				
			// TODO: stop monitoring the file...
		}

		public void Tick()
		{
			// check for file change at regular intervals
		}
	}
}

