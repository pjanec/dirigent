using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using X = Dirigent.XmlConfigReaderUtils;
using System.Diagnostics;

namespace Dirigent
{

	public class LocalConfigReader
	{
		public LocalConfig Config => _cfg;
		LocalConfig _cfg;
		XDocument _doc;
		XElement _root; // the top level XML element
		FileDefReg _fdReg = new FileDefReg();
		string _machineId = string.Empty; // what machine we are loading the config for; empty if unidentified (non-agent) machine
		

		public LocalConfigReader( System.IO.TextReader textReader, string machineId )
		{
			_machineId = machineId ?? string.Empty;
			_doc = XDocument.Load( textReader );

			#pragma warning disable CS8601 // Possible null reference assignment.
			_root = _doc.Element( "Local" );
			#pragma warning restore CS8601 // Possible null reference assignment.
			if ( _root is null ) throw new Exception("LocalConfig missing the root element");
			
			_cfg = new LocalConfig( _doc );

			LoadFolderWatchers();
			LoadTools();
			LoadDefaultActions();

			//loadPlans();
			//loadMachines();
			//loadMaster();
		}

		void LoadFolderWatchers()
		{
			var fwNodes = from e in _root?.Elements( "FolderWatcher" )
						  select e;

			foreach( var fwNode in fwNodes )
			{
				_cfg.folderWatcherXmls.Add( fwNode );
			}
		}

		void LoadTools()
		{
			_cfg.Tools.Clear();

			var tools = from e in _root.Elements( "Tool" )
						select e;

			foreach( var p in tools )
			{
				var toolDef = SharedConfigReader.ReadAppElement( p, _root, _fdReg, null, null );
				_cfg.Tools.Add( toolDef );
			}
		}

		List<ActionDef> LoadActionList( XElement? root )
		{
			var list = new List<ActionDef>();
			if (root is null) return list;

			var elems = from e in root.Elements()	select e;

			foreach( var e in elems )
			{
				var def = SharedConfigReader.LoadAction( e );
				if( def is not  null )
				{
					list.Add( def );
				}
			}
			
			return list;
		}

		void LoadDefaultActions()
		{
			_cfg.DefaultFileActions = LoadActionList( _root.Element( "DefaultFileActions" ) );
			_cfg.DefaultFilePackageActions = LoadActionList( _root.Element( "DefaultFilePackageActions" ) );
			_cfg.DefaultAppActions = LoadActionList( _root.Element( "DefaultAppActions" ) );
			_cfg.DefaultMachineActions = LoadActionList( _root.Element( "DefaultMachineActions" ) );
		}
	}
}
