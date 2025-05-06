using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using YamlDotNet.Serialization;

namespace Eryph.Core.Network;

public class NetworkProvider
{
    public required string Name { get; set; }

    public required NetworkProviderType Type { get; set; }

    [CanBeNull] public string BridgeName { get; set; }

    [CanBeNull] public string SwitchName { get; set; }

    public int? Vlan { get; set; }

    public bool? MacAddressSpoofing { get; set; }

    public bool? DisableDhcpGuard { get; set; }

    public bool? DisableRouterGuard { get; set; }

    [CanBeNull] public NetworkProviderBridgeOptions BridgeOptions { get; set; }

    [CanBeNull] public string[] Adapters { get; set; }

    [CanBeNull] public NetworkProviderSubnet[] Subnets { get; set; }
}
