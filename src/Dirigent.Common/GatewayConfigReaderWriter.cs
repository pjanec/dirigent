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

	public class GatewayConfigReader
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public GatewayConfig Config => _cfg;

		GatewayConfig _cfg;
		XDocument _doc;
		XElement _root; // the top level XML element


		public GatewayConfigReader( string fileName )
		{
			_cfg = new GatewayConfig();
			var reader = System.IO.File.OpenText( fileName );
			_doc = XDocument.Load( reader ); // null should never be returned, exception would be thrown insterad
			_root = _doc.Element( "GatewayConfig" )!;
			if ( _root is null ) throw new Exception("SharedConfig missing the root element");
			_cfg.Gateways = LoadGateways( _root );
		}
		
		List<GatewayDef> LoadGateways( XElement root )
		{
			var res = new List<GatewayDef>();
			var gateways = from e in root.Elements( "Gateway" )
				select e;

			foreach( var p in gateways )
			{
				var g = LoadGateway( p );
				res.Add( g );
			}
			
			return res;
		}

		GatewayDef LoadGateway( XElement p )
		{
			var g = new GatewayDef();
			g.ExternalIP = X.getStringAttr( p, "ExternalIP", "" );
			g.InternalIP = X.getStringAttr( p, "InternalIP", "" );
			g.Port = X.getIntAttr( p, "Port", 0 );
			g.UserName = X.getStringAttr( p, "UserName", "" );
			g.Password = X.getStringAttr( p, "Password", "" );
			g.MasterIP = X.getStringAttr( p, "MasterIP", "" );
			g.MasterPort = X.getIntAttr( p, "MasterPort", 0 );
			g.Machines = LoadMachines( p );
			return g;
		}
		

		MachineDef LoadMachine( XElement e )
		{
			var m = new MachineDef();
			m.IP = X.getStringAttr( e, "IP", "" );
			m.Services = LoadServices( e );
			return m;
		}
		
		List<ServiceDef> LoadServices( XElement root )
		{
			var res = new List<ServiceDef>();
			
			var services = from e in root.Elements( "Services" )
						select e;

			int index = 0;
			foreach( var p in services )
			{
				index++;
				var m = LoadService( p );
				res.Add( m );
			}

			return res;
		}

		ServiceDef LoadService( XElement e )
		{
			var m = new ServiceDef();
			m.Name = X.getStringAttr( e, "Name", "" );
			m.Port = X.getIntAttr( e, "Port", 0 );
			//m.IP = X.getStringAttr( e, "IP", "" );
			//m.IP = X.getStringAttr( e, "IP", "" );
			return m;
		}
		

		List<MachineDef> LoadMachines( XElement root )
		{
			var res = new List<MachineDef>();
			
			var machines = from e in root.Elements( "Machine" )
						select e;

			int index = 0;
			foreach( var p in machines )
			{
				index++;
				var m = LoadMachine( p );
				res.Add( m );
			}

			return res;
		}
		
	}

	public class GatewayConfigWriter
	{
		public void Write( string fileName , GatewayConfig cfg )
		{
			var doc = new XDocument();
			var root = new XElement( "GatewayConfig" );
			doc.Add( root );

			// FIXME: commented out to avoid overwriting our hand-written config until we can safely write it
			//doc.Save( fileName );
		}
	}
}

