using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using YamlDotNet.Serialization;

namespace Eryph.Core.Network;

public class NetworkProvider
{
    public string Name { get; set; }

    [YamlMember(Alias = "Type")]
    public string TypeString { get; set; }

    public string BridgeName { get; set; }
    public string SwitchName { get; set; }

    public NetworkProviderVLan Vlan { get; set; }

    public string[] Adapters { get; set; }

    public NetworkProviderSubnet[] Subnets { get; set; }

    [YamlIgnore] public NetworkProviderType Type => ParseType(TypeString);

    public static NetworkProviderType ParseType([CanBeNull] string typeString)
    {
        if (string.IsNullOrWhiteSpace(typeString))
            return NetworkProviderType.Invalid;

        return typeString switch
        {
            "nat_overlay" => NetworkProviderType.NatOverLay,
            "overlay" => NetworkProviderType.Overlay,
            "flat" => NetworkProviderType.Flat,
            _ => NetworkProviderType.Invalid
        };
    }

    public static Validation<Error, NetworkProvider> Validate(NetworkProvider provider)
    {
        return from nonNullName in string.IsNullOrWhiteSpace(provider.Name)
                ? Prelude.Fail<Error, NetworkProvider>("network provider name is required")
                : Prelude.Success<Error, NetworkProvider>(provider)
               from nonInvalidType in provider.Type == NetworkProviderType.Invalid
                   ? Prelude.Fail<Error, NetworkProvider>($"network_provider {provider.Name}: network provider type has to of value overlay, nat_overlay or flat")
                   : Prelude.Success<Error, NetworkProvider>(provider)
                   let hasTag = provider.Vlan is { Tag: > 0 }
                   from nonInvalidVlan in provider.Type == NetworkProviderType.NatOverLay && hasTag
                          ? Prelude.Fail<Error, NetworkProvider>($"network_provider {provider.Name}: vlan tag is not supported for nat_overlay network providers")
                          : Prelude.Success<Error, NetworkProvider>(provider)
               select provider;

    }

}