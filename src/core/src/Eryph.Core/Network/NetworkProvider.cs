namespace Eryph.Core.Network;

public class NetworkProvider
{
    public required string Name { get; set; }

    public required NetworkProviderType Type { get; set; }

    public string? BridgeName { get; set; }

    public string? SwitchName { get; set; }

    public int? Vlan { get; set; }

    public bool? MacAddressSpoofing { get; set; }

    public bool? DisableDhcpGuard { get; set; }

    public bool? DisableRouterGuard { get; set; }

    public NetworkProviderBridgeOptions? BridgeOptions { get; set; }

    public string[]? Adapters { get; set; }

    public NetworkProviderSubnet[]? Subnets { get; set; }
}
