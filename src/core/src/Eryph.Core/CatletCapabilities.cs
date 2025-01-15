using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using LanguageExt;

namespace Eryph.Core;

public static class CatletCapabilities
{
    public static bool IsDynamicMemoryEnabled(
        Seq<CatletCapabilityConfig> configs) =>
        FindCapability(configs, EryphConstants.Capabilities.DynamicMemory)
            .Map(c => !IsExplicitlyDisabled(c))
            .IfNone(false);

    public static bool IsDynamicMemoryExplicitlyDisabled(
        Seq<CatletCapabilityConfig> configs) =>
        FindCapability(configs, EryphConstants.Capabilities.DynamicMemory)
            .Map(IsExplicitlyDisabled)
            .IfNone(false);

    public static bool IsNestedVirtualizationEnabled(
        Seq<CatletCapabilityConfig> configs) =>
        FindCapability(configs, EryphConstants.Capabilities.NestedVirtualization)
            .Map(c => !IsExplicitlyDisabled(c))
            .IfNone(false);

    public static bool IsTpmEnabled(
        Seq<CatletCapabilityConfig> configs) =>
        FindCapability(configs, EryphConstants.Capabilities.Tpm)
            .Map(c => !IsExplicitlyDisabled(c))
            .IfNone(false);

    public static Option<CatletCapabilityConfig> FindCapability(
        Seq<CatletCapabilityConfig> configs,
        string name) =>
        configs.Find(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    public static bool IsExplicitlyDisabled(
        CatletCapabilityConfig capabilityConfig) =>
        capabilityConfig.Details.ToSeq()
            .Any(d => string.Equals(d, EryphConstants.CapabilityDetails.Disabled, StringComparison.OrdinalIgnoreCase));
}
