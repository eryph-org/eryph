using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Eryph.VmManagement.Networking
{
    public static class NetworkAddresses
    {
        public static string[] GetAddressesByFamily(IEnumerable<string> addresses, AddressFamily family)
        {
            return addresses.Where(a =>
            {
                var ipAddress = IPAddress.Parse(a);
                return ipAddress.AddressFamily == family;
            }).ToArray();
        }


        public static IEnumerable<string> AddressesAndSubnetsToSubnets(IReadOnlyList<string> ipAddresses,
            IReadOnlyList<string> netmasks)
        {
            for (var i = 0; i < ipAddresses.Count; i++)
            {
                var address = ipAddresses[i];
                var netmask = netmasks[i];

                if (netmask.StartsWith("/"))
                    yield return IPNetwork.Parse(address + netmask).ToString();
                else
                {
                    if (byte.TryParse(netmask, out _))
                    {
                        yield return IPNetwork.Parse(address + "/" + netmask).ToString();
                        continue;
                    }

                    yield return IPNetwork.Parse(address, netmask).ToString();
                }
            }
        }
    }
}