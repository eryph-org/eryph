using System;
using System.Collections.Generic;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletNetwork
{
    public required string Name { get; set; }

    public required string Provider { get; set; }

    public IReadOnlyList<string>? IpV4Addresses { get; set; }

    public string? IPv4DefaultGateway { get; set; }

    public IReadOnlyList<string>? DnsServerAddresses { get; set; }

    public IReadOnlyList<string>? IpV4Subnets { get; set; }

    public FloatingNetworkPort? FloatingPort { get; set; }
}
