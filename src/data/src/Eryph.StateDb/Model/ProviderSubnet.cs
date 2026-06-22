namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class ProviderSubnet : Subnet
{
    public required string ProviderName { get; set; }

    // The following settings are only used by flat providers. They are pushed into
    // the catlet's guest network configuration as there is no eryph-managed DHCP
    // server on a flat network. Overlay/NAT providers keep these settings on the
    // virtual network subnet instead.
    public string? Gateway { get; set; }

    public int MTU { get; set; }

    public string? DnsServersV4 { get; set; }
}
