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

public static class NetworkProvidersConfigsValidations
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
            provider => Validations.ValidateDistinct(
                provider, p => NetworkProviderName.NewValidation(p.Name), "provider name"),
            path)
        select unit;

    private static Validation<ValidationIssue, Unit> ValidateNetworkProviderConfig(
        NetworkProvider toValidate,
        string path = "") =>
        ValidateProperty(toValidate, c => c.Name, NetworkProviderName.NewValidation, path, required: true)
        | ValidateProperty(toValidate, c => c.Type, ValidateProviderType, path, required: true)
        | ValidateProperty(toValidate, c => c.BridgeName, ValidateBridgeName, path)
        | ValidateProperty(toValidate, c => c.BridgeOptions,
            (o, p) => ValidateNetworkProviderBridgeOptions(o, toValidate.Type, p), path);

    private static Validation<ValidationIssue, Unit> ValidateNetworkProviderBridgeOptions(
        NetworkProviderBridgeOptions toValidate,
        NetworkProviderType providerType,
        string path = "") =>
        from _1 in guard(providerType is NetworkProviderType.Overlay,
                new ValidationIssue(path, "Bridge options are only supported for overlay providers without NAT."))
            .ToValidation()
        from _2 in ValidateProperty(toValidate, c => c.BridgeVlan, ValidateVlanTag, path)
                   | ValidateProperty(toValidate, c => c.VlanMode, ValidateVlanMode, path)
        select unit;

    private static Validation<Error, BridgeName> ValidateBridgeName(
        string bridgeName) =>
        from parsedName in BridgeName.NewValidation(bridgeName)
        from _ in guardnot(parsedName == BridgeName.New("br-int"),
                Error.New("The bridge name 'br-int' is reserved."))
            .ToValidation()
        select parsedName;

    private static Validation<Error, Unit> ValidateProviderType(
        NetworkProviderType providerType) =>
        guard(providerType is NetworkProviderType.Flat
                    or NetworkProviderType.Overlay
                    or NetworkProviderType.NatOverlay,
                Error.New($"The provider type '{providerType}' is not supported."))
            .ToValidation();

    private static Validation<Error, Unit> ValidateVlanTag(int vlanTag) =>
        guard(vlanTag > 0, Error.New("The vlan tag must be greater than 0."))
            .ToValidation();

    private static Validation<Error, Unit> ValidateVlanMode(
        BridgeVlanMode vlanMode) =>
        guard(vlanMode is BridgeVlanMode.Access
                    or BridgeVlanMode.NativeTagged
                    or BridgeVlanMode.NativeUntagged,
                Error.New($"The VLAN mode '{vlanMode}' is not supported."))
            .ToValidation();
}
