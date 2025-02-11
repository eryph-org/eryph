using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using JetBrains.Annotations;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public static class CatletConfigDefaults
{
    /// <summary>
    /// Applies hardcoded default values to the <paramref name="config"/>.
    /// </summary>
    public static CatletConfig ApplyDefaults(CatletConfig config) =>
        config.CloneWith(c =>
        {
            c.Name = Optional(c.Name)
                .Filter(notEmpty)
                .IfNone(EryphConstants.DefaultCatletName);
            c.Cpu = ApplyCpuDefaults(c.Cpu);
            c.Memory = ApplyMemoryDefaults(c.Memory);
            c.Networks = ApplyNetworksDefaults(Seq(c.Networks)).ToArray();
        });

    private static CatletCpuConfig ApplyCpuDefaults(
        Option<CatletCpuConfig> config) =>
        config.Filter(c => c.Count.HasValue)
            .Map(c => c.Clone())
            .IfNone(new CatletCpuConfig
            {
                Count = EryphConstants.DefaultCatletCpuCount,
            });

    private static CatletMemoryConfig ApplyMemoryDefaults(
        Option<CatletMemoryConfig> config) =>
        config.Filter(c => c.Startup.HasValue || c.Minimum.HasValue || c.Maximum.HasValue)
            .Map(c => c.Clone())
            .IfNone(new CatletMemoryConfig
            {
                Startup = EryphConstants.DefaultCatletMemoryMb,
            });

    private static Seq<CatletNetworkConfig> ApplyNetworksDefaults(
        Seq<CatletNetworkConfig> configs) =>
        configs.DefaultIfEmpty(new CatletNetworkConfig { Name = EryphConstants.DefaultNetworkName })
            .Map(ApplyNetworkDefaults)
            .ToSeq();

    private static CatletNetworkConfig ApplyNetworkDefaults(
        int index,
        CatletNetworkConfig config) =>
        config.CloneWith(c =>
        {
            c.AdapterName = Optional(c.AdapterName)
                .Filter(notEmpty)
                .IfNone($"eth{index}");
        });
}
