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
    public static CatletConfig Minimize(CatletConfig config) =>
        config.CloneWith(c =>
        {
            c.Hostname = c.Hostname != c.Name ? c.Hostname : null;
            c.Cpu = Minimize(c.Cpu);
            c.Memory = Minimize(c.Memory);
            c.Capabilities = Minimize(c.Capabilities);
            c.Drives = Minimize(c.Drives);
            c.NetworkAdapters = Minimize(c.NetworkAdapters);
            c.Networks = Minimize(c.Networks);
            c.Variables = Minimize(c.Variables);
            c.Fodder = Minimize(c.Fodder);
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
        [CanBeNull] FodderConfig[] configs) =>
        Optional(configs).Filter(c => c.Length > 0)
            .Map(s => s.Map(Minimize))
            .Map(s => s.ToArray())
            .IfNoneUnsafe(() => null);

    [CanBeNull]
    private static FodderConfig Minimize(FodderConfig config) =>
        config.CloneWith(c =>
        {
            c.Variables = Minimize(c.Variables);
        });

    [CanBeNull]
    private static T[] Minimize<T>(
        [CanBeNull] T[] configs)
        where T : ICloneableConfig<T> =>
        Optional(configs).Filter(c => c.Length > 0)
            .Map(s => s.Map(c => c.Clone()))
            .Map(s => s.ToArray())
            .IfNoneUnsafe(() => null);
}
