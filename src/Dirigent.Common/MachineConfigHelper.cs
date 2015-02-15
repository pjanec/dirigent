using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.NetworkInformation;

namespace Dirigent.Common
{
    public class MachineConfigHelper
    {
        //public static string autoconfigMachineIdFromIpAddress( IEnumerable<MachineDef> machines )
        //{
        //    // convert ip adresses from textual form to IPAddress
        //    Dictionary<System.Net.IPAddress, string> ipToMachineId = new Dictionary<System.Net.IPAddress, string>();
        //    foreach( var m in machines )
        //    {
        //        var ipaddr = System.Net.IPAddress.Parse(m.IpAddress);
        //        ipToMachineId[ipaddr] = m.MachineId;
        //    }
            
        //    // traverse all network interfaces on this machine and compare the IP address
        //    foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        //    {
        //       if(ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
        //       {
        //           Console.WriteLine(ni.Name);
        //           foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
        //           {
        //               if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        //               {
        //                   Console.WriteLine(ip.Address.ToString());

        //                   if( ipToMachineId.ContainsKey(ip.Address) )
        //                   {
        //                        return ipToMachineId[ip.Address];
        //                   }
        //               }
        //           }
        //       }  
        //    }

        //    return null;
        //}


    }
}
