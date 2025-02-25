using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

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
        string path = "") =>
        from _1 in ValidateList(toValidate, c => c.NetworkProviders,
            ValidateNetworkProviderConfig, path, minCount: 1, maxCount: int.MaxValue)
        from _2 in ValidateProperty(toValidate, c => c.NetworkProviders,
            providers => Validations.ValidateDistinct(
                providers, p => NetworkProviderName.NewValidation(p.Name), "network provider name"),
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
                Success<Error, string>, "adapter"),
            path)
        select unit;

    private static Validation<ValidationIssue, Unit> ValidateNetworkProviderConfig(
        NetworkProvider toValidate,
        string path = "") =>
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
        string path = "") =>
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
        string path = "") =>
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
            path);

    private static Validation<ValidationIssue, Unit> ValidateOverlayProviderConfig(
        NetworkProvider toValidate,
        string path = "") =>
        ValidateProperty(toValidate, c => c.BridgeName, ValidateBridgeName, path, required: true)
        | ValidateProperty(toValidate, c => c.SwitchName,
            NotAllowed<string>("The overlay network provider does not support custom switch names."),
            path)
        | ValidateProperty(toValidate, c => c.BridgeOptions, ValidateNetworkProviderBridgeOptions, path)
        | ValidateProperty(toValidate, c => c.Vlan, ValidateVlanTag, path);

    private static Validation<ValidationIssue, Unit> ValidateNetworkProviderBridgeOptions(
        NetworkProviderBridgeOptions toValidate,
        string path = "") =>
        ValidateProperty(toValidate, c => c.BridgeVlan, ValidateVlanTag, path);

    private static Validation<ValidationIssue, Unit> ValidateSubnet(
        NetworkProviderSubnet toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.Name, ValidateSubnetName, path, required: true)
        | ValidateList(toValidate, c => c.IpPools, ValidateIpPool, path, minCount: 1);

    private static Validation<ValidationIssue, Unit> ValidateIpPool(
        NetworkProviderIpPool toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.Name, ValidateIpPoolName, path, required: true);

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

    private static Validation<Error, Unit> ValidateVlanTag(int vlanTag) =>
        guard(vlanTag > 0, Error.New("The VLAN tag must be greater than 0."))
            .ToValidation()
        | guard(vlanTag < 4096, Error.New("The VLAN tag must be less than 4096."))
            .ToValidation();

    private static Func<T, Validation<Error, T>> NotAllowed<T>(
        string errorMessage) =>
        _ => Fail<Error, T>(Error.New(errorMessage));
}
