using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace Eryph.Core.Network;

public class NetworkProviderBridgeOptions
{
    // this is intentionally not named vlan to avoid confusion with the vlan of the provider
    public int? BridgeVlan { get; set; }

    [YamlMember(Alias = "vlan_mode")]
    public string VLanModeString { get; set; }

    [YamlIgnore] public BridgeVlanMode VLanMode => ParseVLanMode(VLanModeString);

    public static BridgeVlanMode ParseVLanMode([CanBeNull] string modeString)
    {
        if (string.IsNullOrWhiteSpace(modeString))
            return BridgeVlanMode.Invalid;

        return modeString switch
        {
            "access" => BridgeVlanMode.Access,
            "native_untagged" => BridgeVlanMode.NativeUntagged,
            "native_tagged" => BridgeVlanMode.NativeTagged,
            _ => BridgeVlanMode.Invalid
    };
    }

    // the ip mode is only used when the bridge is created, therefore it is
    // named default_ip_mode
    [YamlMember(Alias = "default_ip_mode")]
    public string DefaultIpModeString { get; set; }

    [YamlIgnore] public BridgeHostIpMode DefaultIpMode => ParseHostIpMode(DefaultIpModeString);

    public static BridgeHostIpMode ParseHostIpMode([CanBeNull] string modeString)
    {
        if (string.IsNullOrWhiteSpace(modeString))
            return BridgeHostIpMode.Disabled;

        return modeString switch
        {
            // for future use
            //"static" => BridgeHostIpMode.Static,
            "dhcp" => BridgeHostIpMode.Dhcp,
            "disabled" => BridgeHostIpMode.Disabled,
            _ => BridgeHostIpMode.Disabled
        };
    }


}