using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Networks;

namespace Eryph.Core.Network;

public static class ProjectNetworksConfigDefault
{
    public static ProjectNetworksConfig Default => new()
    {
        Networks = new NetworkConfig[]
        {
            new()
            {
                Name = "default",
                Environment = "default",
                Provider = new ProviderConfig()
                {
                    Name = "default",
                    Subnet = "default",
                    IpPool = "default",
                },
                Address = "10.0.0.0/20",
                Subnets = new NetworkSubnetConfig[]
                {
                    new()
                    {
                        Name = "default",
                        Address = "10.0.0.0/20",
                        DnsServers = new[] { "9.9.9.9", "8.8.8.8" },
                        Mtu = 1400,
                        IpPools = new IpPoolConfig[]
                        {
                            new()
                            {
                                Name = "default",
                                FirstIp = "10.0.0.100",
                                NextIp = "10.0.0.100",
                                LastIp = "10.0.0.240"
                            }
                        },
                    },
                },
            },
        },
    };
}
