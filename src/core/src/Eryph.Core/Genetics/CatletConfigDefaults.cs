using System.Linq;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public static class CatletConfigDefaults
{
    /// <summary>
    /// Applies the default network in case the <paramref name="config"/>
    /// contains no networks at all.
    /// </summary>
    /// <remarks>
    /// The default network must be added before the breeding. This way,
    /// the default network can still be explicitly removed by using the
    /// <see cref="MutationType.Remove"/> mutation.
    /// </remarks>
    public static CatletConfig ApplyDefaultNetwork(CatletConfig config) =>
        config.CloneWith(c =>
        {
            c.Networks = c.Networks.ToSeq()
                .DefaultIfEmpty(new CatletNetworkConfig { Name = EryphConstants.DefaultNetworkName })
                .ToArray();
        });

    /// <summary>
    /// Applies hardcoded default values to the <paramref name="config"/>.
    /// </summary>
    /// <remarks>
    /// These default values must be applied in the very end after the
    /// breeding. This way, the catlet will always use reasonable default
    /// values.
    /// </remarks>
    public static CatletConfig ApplyDefaults(CatletConfig config) =>
        config.CloneWith(c =>
        {
            c.Name = Optional(c.Name)
                .Filter(notEmpty)
                .IfNone(EryphConstants.DefaultCatletName);
            c.Cpu = ApplyCpuDefaults(c.Cpu);
            c.Memory = ApplyMemoryDefaults(c.Memory);

            var networks = c.Networks.ToSeq().Map(ApplyNetworkDefaults).ToSeq();
            var additionalAdapters = networks
                .Map(n => n.AdapterName)
                .Except(c.NetworkAdapters.ToSeq().Map(a => a.Name))
                .Map(name => new CatletNetworkAdapterConfig()
                {
                    Name = name,
                });

            c.Networks = c.Networks.ToSeq().Map(ApplyNetworkDefaults).ToArray();
            c.NetworkAdapters = c.NetworkAdapters.ToSeq()
                .Concat(additionalAdapters)
                .ToArray();
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
