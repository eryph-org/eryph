using System.Net;
using System.Net.NetworkInformation;

namespace Haipa.Modules.SSL
{
    public static class Network
    {
        public static string FQDN
        {
            get
            {
                string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
                string hostName = Dns.GetHostName();

                if (!hostName.EndsWith(domainName))
                {
                    hostName += "." + domainName;
                }
                return hostName;
            }
        }
    }
}
