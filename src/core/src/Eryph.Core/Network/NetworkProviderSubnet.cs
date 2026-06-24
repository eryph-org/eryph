namespace Eryph.Core.Network;

public class NetworkProviderSubnet
{
    public string? Name { get; set; }
    public string? Network { get; set; }
    public string? Gateway { get; set; }

    // The following settings are only used by flat providers. They are pushed
    // into the catlet's guest network configuration (cloud-init) as there is no
    // eryph-managed DHCP server on a flat network to hand them out.
    public string[]? DnsServers { get; set; }
    public string? DnsDomain { get; set; }
    public int? Mtu { get; set; }

    public NetworkProviderIpPool[]? IpPools { get; set; }
}
