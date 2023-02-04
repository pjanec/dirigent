using System.Xml.Serialization;
namespace Dirigent
{
	/// <summary>
	/// Network service runningn on a port we want to access via port forwarding
	/// </summary>
	public class ServiceDef
    {
		[XmlAttribute]
        public string Name="";

		[XmlAttribute]
        public int Port;
        //public string UserName;
        //public string Password;
    }
}
