using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace Eryph.Core.Network;

public class NetworkProviderBridgeOptions
{
    // this is intentionally not named vlan to avoid confusion with the vlan of the provider
    public int? BridgeVlan { get; set; }

    public BridgeVlanMode? VlanMode { get; set; }

    // the ip mode is only used when the bridge is created, therefore it is
    // named default_ip_mode
    public BridgeHostIpMode? DefaultIpMode { get; set; }

    public BondMode? BondMode { get; set; }
}
