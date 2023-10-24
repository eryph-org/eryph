using System.Collections.Generic;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class FloatingNetworkPort
{
    public string Name { get; set; }
    public string Provider { get; set; }
    public string Subnet { get; set; }

    public IEnumerable<string> IpV4Addresses { get; set; }

    public IEnumerable<string> IpV4Subnets { get; set; }

}