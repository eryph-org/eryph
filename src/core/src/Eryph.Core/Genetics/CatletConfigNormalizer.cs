using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

#nullable enable

public static class CatletConfigNormalizer
{
    public static Validation<Error, CatletConfig> Normalize(
        CatletConfig catletConfig) =>
        from catletName in Optional(catletConfig.Name).Filter(notEmpty).Match(
            Some: CatletName.NewValidation,
            None: () => CatletName.New(EryphConstants.DefaultCatletName))
        from parent in Optional(catletConfig.Parent)
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewValidation)
            .Sequence()
        let hostName = Optional(catletConfig.Hostname).Filter(notEmpty).IfNone(catletName.Value)
        from projectName in Optional(catletConfig.Project).Filter(notEmpty).Match(
            Some: ProjectName.NewValidation,
            None: () => ProjectName.New(EryphConstants.DefaultProjectName))
        from dataStoreName in Optional(catletConfig.Store).Filter(notEmpty).Match(
            Some: DataStoreName.NewValidation,
            None: () => DataStoreName.New(EryphConstants.DefaultDataStoreName))
        from environmentName in Optional(catletConfig.Environment).Filter(notEmpty).Match(
            Some: EnvironmentName.NewValidation,
            None: () => EnvironmentName.New(EryphConstants.DefaultEnvironmentName))
        from storageIdentifier in Optional(catletConfig.Location)
            .Filter(notEmpty)
            .Map(StorageIdentifier.NewValidation)
            .Sequence()
            // TODO implement additional normalization
            // TODO normalization of MAC addresses?
        select catletConfig.CloneWith(c =>
        {
            c.Name = catletName.Value;
            c.Parent = parent.Map(p => p.Value).IfNoneUnsafe((string?)null);
            c.Hostname = hostName;
            c.Project = projectName.Value;
            c.Environment = environmentName.Value;
            c.Store = dataStoreName.Value;
            c.Location = storageIdentifier.Map(s => s.Value).IfNoneUnsafe((string?)null);
        });

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
