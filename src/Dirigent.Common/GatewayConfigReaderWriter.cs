using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Xml;
using System.Xml.Serialization;

namespace Dirigent
{

	public class GatewayConfigReader
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public GatewayConfig Config => _cfg;

		GatewayConfig _cfg;


		public GatewayConfigReader( string fileName )
		{
			_cfg = new GatewayConfig();

			using (XmlReader xr = XmlReader.Create( fileName ))
			{
				var x = new XmlSerializer( _cfg.GetType() );
				_cfg = (x.Deserialize( xr ) as GatewayConfig) ?? throw new Exception( "Failed to deserialize GatewayConfig" );
			}

		}
	}

	public class GatewayConfigWriter
	{
		public void Write( string fileName , GatewayConfig cfg )
		{

			XmlWriterSettings xws = new XmlWriterSettings();  
			xws.Indent = true;
			xws.Encoding = Encoding.UTF8;
			using (XmlWriter xw = XmlWriter.Create( fileName, xws))
			{  
				var x = new XmlSerializer(cfg.GetType());
				x.Serialize( xw, cfg );
			}
		}
	}
}

