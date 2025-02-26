using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

using static Eryph.Core.NetworkPrelude;
using static LanguageExt.Prelude;
using static Dbosoft.Functional.Validations.ComplexValidations;

namespace Eryph.Core.Network;

/// <summary>
/// Validates the <see cref="NetworkProvidersConfiguration"/>.
/// </summary>
/// <remarks>
/// The validation can be improved in the future when the validation
/// for the project network configuration has been implemented.
/// </remarks>
public static class NetworkProvidersConfigValidations
{
    public static Validation<ValidationIssue, Unit> ValidateNetworkProvidersConfig(
        NetworkProvidersConfiguration toValidate,
        string path = "") =>
        ValidateNetworkProviderConfigs(toValidate, path);
        
    private static Validation<ValidationIssue, Unit> ValidateNetworkProviderConfigs(
        NetworkProvidersConfiguration toValidate,
        string path) =>
        from _1 in ValidateList(toValidate, c => c.NetworkProviders,
            ValidateNetworkProviderConfig, path, minCount: 1, maxCount: int.MaxValue)
        from _2 in ValidateProperty(toValidate, c => c.NetworkProviders,
            providers => Validations.ValidateDistinct(
                providers,
                p => NetworkProviderName.NewValidation(p.Name),
                "network provider name"),
            path)
        from _3 in ValidateProperty(toValidate, c => c.NetworkProviders,
            providers => Validations.ValidateDistinct(
                providers.ToSeq().Bind(p =>Optional(p.BridgeName).ToSeq()),
                BridgeName.NewValidation,
                "bridge name"),
            path)
        from _4 in ValidateProperty(toValidate, c => c.NetworkProviders,
            providers => Validations.ValidateDistinct(
                providers.ToSeq().Bind(p => p.Adapters.ToSeq()),
                Success<Error, string>,
                "adapter"),
            path)
        from _5 in ValidateProperty(toValidate, c => c.NetworkProviders,
            providers => ValidateNoOverlappingNatNetworks(providers.ToSeq()),
            path)
        select unit;

    private static Validation<ValidationIssue, Unit> ValidateNetworkProviderConfig(
        NetworkProvider toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.Name, NetworkProviderName.NewValidation, path, required: true)
        | ValidateList(toValidate, c => c.Subnets, ValidateSubnet, path, minCount: 1)
        | toValidate.Type switch
        {
            NetworkProviderType.Flat => ValidateFlatProviderConfig(toValidate, path),
            NetworkProviderType.Overlay => ValidateOverlayProviderConfig(toValidate, path),
            NetworkProviderType.NatOverlay => ValidateNatOverlayProviderConfig(toValidate, path),
            _ => Success<ValidationIssue, Unit>(unit),
        };

    private static Validation<ValidationIssue, Unit> ValidateFlatProviderConfig(
        NetworkProvider toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.SwitchName, ValidateSwitchName, path, required: true)
        | ValidateProperty(toValidate, c => c.BridgeName,
            NotAllowed<string>("The flat network provider does not use the bridge name."),
            path)
        | ValidateProperty(toValidate, c => c.BridgeOptions,
            NotAllowed<NetworkProviderBridgeOptions>("The flat network provider does not support bridge options."),
            path)
        | ValidateProperty(toValidate, c => c.Adapters,
            NotAllowed<string[]>("The flat network provider does not use adapters."),
            path)
        | ValidateProperty(toValidate, c => c.Vlan,
            NotAllowed<int>("The flat network provider does not support the configuration of a VLAN."),
            path);

    private static Validation<ValidationIssue, Unit> ValidateNatOverlayProviderConfig(
        NetworkProvider toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.BridgeName, ValidateBridgeName, path, required: true)
        | ValidateProperty(toValidate, c => c.SwitchName,
            NotAllowed<string>("The NAT overlay network provider does not support custom switch names."),
            path)
        | ValidateProperty(toValidate, c => c.BridgeOptions,
            NotAllowed<NetworkProviderBridgeOptions>(
                "The NAT overlay network provider does not support bridge options."),
            path)
        | ValidateProperty(toValidate, c => c.Adapters,
            NotAllowed<string[]>("The NAT overlay network provider does not use adapters."),
            path)
        | ValidateProperty(toValidate, c => c.Vlan,
            NotAllowed<int>("The NAT overlay network provider does not support the configuration of a VLAN."),
            path)
        | ValidateProperty(toValidate, c => c.Subnets, ValidateNatProviderSubnets, path);

    private static Validation<ValidationIssue, Unit> ValidateOverlayProviderConfig(
        NetworkProvider toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.BridgeName, ValidateBridgeName, path, required: true)
        | ValidateProperty(toValidate, c => c.SwitchName,
            NotAllowed<string>("The overlay network provider does not support custom switch names."),
            path)
        | ValidateProperty(toValidate, c => c.BridgeOptions, ValidateNetworkProviderBridgeOptions, path)
        | ValidateProperty(toValidate, c => c.Vlan, ValidateVlanTag, path);

    private static Validation<ValidationIssue, Unit> ValidateNetworkProviderBridgeOptions(
        NetworkProviderBridgeOptions toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.BridgeVlan, ValidateVlanTag, path);

    private static Validation<ValidationIssue, Unit> ValidateSubnet(
        NetworkProviderSubnet toValidate,
        string path) =>
        from _1 in ValidateProperty(toValidate, c => c.Name, ValidateSubnetName, path, required: true)
                   | ValidateProperty(toValidate, c => c.Network, ValidateIpNetwork, path, required: true)
        from ipNetwork in parseIPNetwork2(toValidate.Network).ToValidation(
            new ValidationIssue(path, $"The network '{toValidate.Network}' is invalid."))
        from _2 in ValidateProperty(toValidate, c => c.Gateway, i => ValidateIpAddress(i, ipNetwork), path, required: true)
                   | ValidateList(toValidate, c => c.IpPools, (i, p) => ValidateIpPool(i, ipNetwork, p), path, minCount: 1)
        select unit;

    private static Validation<ValidationIssue, Unit> ValidateIpPool(
        NetworkProviderIpPool toValidate,
        IPNetwork2 ipNetwork,
        string path) =>
        ValidateProperty(toValidate, c => c.Name, ValidateIpPoolName, path, required: true)
        | ValidateProperty(toValidate, c => c.FirstIp, i => ValidateIpAddress(i, ipNetwork), path, required: true)
        | ValidateProperty(toValidate, c => c.NextIp, i => ValidateIpAddress(i, ipNetwork), path, required: false)
        | ValidateProperty(toValidate, c => c.LastIp, i => ValidateIpAddress(i, ipNetwork), path, required: true);

    private static Validation<Error, BridgeName> ValidateBridgeName(
        string bridgeName) =>
        from parsedName in BridgeName.NewValidation(bridgeName)
        from _ in guardnot(parsedName == BridgeName.New("br-int"),
                Error.New("The bridge name 'br-int' is reserved."))
            .ToValidation()
        select parsedName;

    private static Validation<Error, string> ValidateSwitchName(
        string switchName) =>
        from _ in Validations.ValidateNotEmpty(switchName, "switch name")
        select switchName;

    private static Validation<Error, string> ValidateSubnetName(
        string subnetName) =>
        from _ in Validations.ValidateNotEmpty(subnetName, "subnet name")
        select subnetName;

    private static Validation<Error, string> ValidateIpPoolName(
        string ipPoolName) =>
        from _ in Validations.ValidateNotEmpty(ipPoolName, "ip pool name")
        select ipPoolName;

    private static Validation<Error, IPAddress> ValidateIpAddress(
        string ipAddress,
        IPNetwork2 ipNetwork) =>
        from parsedIpAddress in parseIPAddress(ipAddress).ToValidation(
            Error.New($"The IP address '{ipAddress}' is invalid."))
        from _ in guard(ipNetwork.Contains(parsedIpAddress),
            Error.New($"The IP address '{ipAddress}' is not part of the provider's network '{ipNetwork}'."))
        select parsedIpAddress;

    private static Validation<Error, IPNetwork2> ValidateIpNetwork(
        string ipNetwork) =>
        from parsedNetwork in parseIPNetwork2(ipNetwork).ToValidation(
            Error.New($"The IP network '{ipNetwork}' is invalid."))
        from _ in guard(ipNetwork == parsedNetwork.ToString(), Error.New(
            $"The normalized IP network '{parsedNetwork}' does not match the specified network '{ipNetwork}'."
            + " The first IP address of the network must be used to specify the network."))
        select parsedNetwork;

    private static Validation<Error, Unit> ValidateVlanTag(int vlanTag) =>
        guard(vlanTag > 0, Error.New("The VLAN tag must be greater than 0."))
            .ToValidation()
        | guard(vlanTag < 4096, Error.New("The VLAN tag must be less than 4096."))
            .ToValidation();

    /// <summary>
    /// Validates that the networks of the different NAT overlay providers do not overlap.
    /// </summary>
    /// <remarks>
    /// We only check NAT overlay providers for overlapping networks. For NAT providers,
    /// overlapping networks cannot be configured at all as the networks would conflict
    /// on the host. For overlay providers, overlapping networks are possible even if
    /// such a configuration is not reasonable in most cases.
    /// </remarks>
    private static Validation<Error, Unit> ValidateNoOverlappingNatNetworks(
        Seq<NetworkProvider> toValidate) =>
        from _1 in Success<Error, Unit>(unit)
        let networks = toValidate
            .Filter(p => p.Type is NetworkProviderType.NatOverlay)
            .Map(p  => p.Subnets.ToSeq()
                .Find(s => s.Name == EryphConstants.DefaultSubnetName)
                .Map(s => s.Network)
                .Bind(parseIPNetwork2)
                .Map(n => (p.Name, Network: n)))
            .Somes()
        from _2 in networks.Zip(
            networks.NonEmptyTails,
            (n, others) => others
                .Filter(o => o.Name != n.Name && n.Network.Overlap(o.Network))
                .Map(o => Fail<Error, Unit>(Error.New(
                    $"The network '{n.Network}' of provider '{n.Name}' overlaps with the network '{o.Network}' of provider '{o.Name}'."))))
            .Flatten()
            .Sequence()
        select unit;

    private static Validation<Error, Unit> ValidateNatProviderSubnets(
        [CanBeNull] NetworkProviderSubnet[] toValidate) =>
        guard(toValidate is { Length: 1 }
              && toValidate[0].Name == EryphConstants.DefaultSubnetName
              && toValidate[0].IpPools is {Length: 1}
              && toValidate[0].IpPools[0].Name == EryphConstants.DefaultIpPoolName,
                Error.New("The NAT overlay provider must contain only the default subnet with the default IP pool."))
            .ToValidation();

    private static Func<T, Validation<Error, T>> NotAllowed<T>(
        string errorMessage) =>
        _ => Fail<Error, T>(Error.New(errorMessage));
}
