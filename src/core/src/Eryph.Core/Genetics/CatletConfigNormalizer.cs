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

public static class CatletConfigNormalizer
{
    /// <summary>
    /// Minimizes the given <paramref name="config"/>.
    /// </summary>
    /// <remarks>
    /// This method removes values which do not contain any
    /// meaningful information (e.g. empty lists). Additionally,
    /// some values are removed when they are identical to other
    /// values.
    /// </remarks>
    public static CatletConfig Minimize(CatletConfig config) =>
        config.CloneWith(c =>
        {
            c.Hostname = c.Hostname != c.Name ? c.Hostname : null;
            c.Cpu = Minimize(c.Cpu);
            c.Memory = Minimize(c.Memory);
            c.Capabilities = Minimize(Seq(c.Capabilities));
            c.Drives = Minimize(Seq(c.Drives));
            c.NetworkAdapters = Minimize(Seq(c.NetworkAdapters));
            c.Networks = Minimize(Seq(c.Networks));
            c.Variables = Minimize(Seq(c.Variables));
            c.Fodder = Minimize(Seq(c.Fodder));
        });

    [CanBeNull]
    private static CatletCpuConfig Minimize(
        Option<CatletCpuConfig> config) =>
        config.Filter(c => c.Count.HasValue)
            .Map(c => c.Clone())
            .IfNoneUnsafe((CatletCpuConfig)null);

    [CanBeNull]
    private static CatletMemoryConfig Minimize(
        Option<CatletMemoryConfig> config) =>
        config.Filter(c => c.Startup.HasValue || c.Minimum.HasValue || c.Maximum.HasValue)
            .Map(c => c.Clone())
            .IfNoneUnsafe((CatletMemoryConfig)null);

    [CanBeNull]
    private static FodderConfig[] Minimize(
        Seq<FodderConfig> configs) =>
        configs.Match(
            Empty: () => null,
            Seq: s => s.Map(Minimize).ToArray());

    [CanBeNull]
    private static FodderConfig Minimize(FodderConfig config) =>
        config.CloneWith(c => { c.Variables = Minimize(Seq(c.Variables)); });

    [CanBeNull]
    private static T[] Minimize<T>(
        Seq<T> configs)
        where T : ICloneableConfig<T> =>
        configs.Match(
            Empty: () => null,
            Seq: s => s.Map(c => c.Clone()).ToArray());
}
