using System.Collections.Generic;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class FloatingNetworkPort
{
    public required string Name { get; set; }
    
    public required string Provider { get; set; }
    
    public required string Subnet { get; set; }

    public IReadOnlyList<string>? IpV4Addresses { get; set; }

    public IReadOnlyList<string>? IpV4Subnets { get; set; }
}
