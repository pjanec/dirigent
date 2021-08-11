using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Dirigent.Common;

using X = Dirigent.XmlConfigReaderUtils;

namespace Dirigent
{
    // watches given folders for creating certain file types and fires event
	public class FolderWatcher
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

		//public delegate void FileChangedDeleg( string fileName, int flags ); // called when a file is changed in given folder
		//public event FileChangedDeleg FileChanged;

		FileSystemWatcher _watcher = new FileSystemWatcher();

		
		public bool Initialized { get; protected set; }
		private string _conditions;
		private List<System.Xml.Linq.XElement> _actionXmls = new List<System.Xml.Linq.XElement>();
		private IDirig _ctrl;
		private string _relativePathsRoot;

        public FolderWatcher( System.Xml.Linq.XElement rootXml, IDirig ctrl, string rootForRelativePaths )
        {
			
			this._ctrl = ctrl;
			this._conditions = X.getStringAttr( rootXml, "Conditions" );
			this._relativePathsRoot = rootForRelativePaths;

            if (String.IsNullOrEmpty(rootForRelativePaths))
            {
                _relativePathsRoot = System.IO.Directory.GetCurrentDirectory();
            }
			else
			{
				_relativePathsRoot = rootForRelativePaths;
			}

			var inclSubdirs = X.getBoolAttr(rootXml, "IncludeSubdirs");
			var path = X.getStringAttr(rootXml, "Path");
			var filter = X.getStringAttr(rootXml, "Filter");

            if( String.IsNullOrEmpty(path) )
			{
				log.Error("Path not defined or empty!");
				return;
			}

			var absPath = BuildAbsolutePath( path );

            if( !System.IO.Directory.Exists(absPath) )
			{
				log.Error($"Path '{absPath}' does not exist! FolderWatcher not installed.");
				return;
			}

			_watcher.Path = absPath;
			_watcher.IncludeSubdirectories = inclSubdirs;

            if( !String.IsNullOrEmpty(filter) )
			{
				_watcher.Filter = filter;
			}

			if( _conditions=="NewFile" )
			{
				_watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite;
				_watcher.Created += new FileSystemEventHandler(OnFileCreated);
			}
			
			foreach( var actXml in rootXml.Descendants("Action") )
			{
				_actionXmls.Add( actXml );
			}

			log.DebugFormat("FolderWatcher initialized. Path={0}, Filter={1}, Conditions={2}", _watcher.Path, _watcher.Filter, _conditions);
			Initialized = true;

			_watcher.EnableRaisingEvents = true;
        }

        string BuildAbsolutePath( string anyPath )
        {
            if( Path.IsPathRooted( anyPath ) )
                return anyPath;

            return Path.Combine( _relativePathsRoot, anyPath );
        }

		private void OnFileCreated(object sender, FileSystemEventArgs e)
		{
			if( _conditions=="NewFile" )
			{
				if (e.ChangeType == WatcherChangeTypes.Created)
				{
					log.InfoFormat("New file detected: {0}", e.FullPath);
					RunActions();
				}
			}
		}

		void RunActions()
		{
			// all actions from XML
			foreach( var actXml in _actionXmls )
			{
				var type = X.getStringAttr(actXml, "Type");
				
				if( type.ToLower() == "StartPlan".ToLower() )
				{
					var planName = X.getStringAttr(actXml, "PlanName");
					
					log.DebugFormat("Running action: StartPlan '{0}'", planName);
					
					_ctrl.Send( new Net.StartPlanMessage( _ctrl.Name, planName ) );
				}
				else if( type.ToLower() == "LaunchApp".ToLower() )
				{
					var appIdTupleStr = X.getStringAttr(actXml, "AppIdTuple");
					
					log.DebugFormat("Running action: LauchApp '{0}'", appIdTupleStr);

					var (appIdTuple, planName) = Tools.ParseAppIdWithPlan( appIdTupleStr );

					_ctrl.Send( new Net.StartAppMessage( _ctrl.Name, appIdTuple, planName ) );
				}
			}
		}
}

}