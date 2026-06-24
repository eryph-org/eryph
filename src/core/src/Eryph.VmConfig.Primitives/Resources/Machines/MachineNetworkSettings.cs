using System.Collections.Generic;

namespace Eryph.Resources.Machines;

public sealed class MachineNetworkSettings
{
    public string AdapterName { get; set; }

    public string NetworkName { get; set; }

    public string NetworkProviderName { get; set; }

    public string PortName { get; set; }

    public string MacAddress { get; set; }

    public IReadOnlyList<string> AddressesV4 { get; set; }

    public IReadOnlyList<string> AddressesV6 { get; set; }

    public string FloatingAddressV4 { get; set; }

    public string FloatingAddressV6 { get; set; }

    public bool MacAddressSpoofing { get; set; }

    public bool DhcpGuard { get; set; }

    public bool RouterGuard { get; set; }

    // Static network settings for catlets on flat networks. When a gateway is set,
    // the addresses are configured statically in the guest (cloud-init) instead of via
    // DHCP, as a flat network has no eryph-managed DHCP server. Sourced from the flat
    // provider subnet. Null/0 for overlay and NAT networks (which rely on eryph DHCP).
    public int? PrefixLengthV4 { get; set; }

    public string GatewayV4 { get; set; }

    public IReadOnlyList<string> DnsServersV4 { get; set; }

    public string DnsDomain { get; set; }

    public int? Mtu { get; set; }
}
