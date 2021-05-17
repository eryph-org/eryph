using System.Net;
using System.Net.NetworkInformation;

namespace Haipa.Security.Cryptography
{
    public static class Network
    {
        public static string FQDN
        {
            get
            {
                var domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
                var hostName = Dns.GetHostName();

                if (!hostName.EndsWith(domainName)) hostName += "." + domainName;
                return hostName;
            }
        }
    }
}