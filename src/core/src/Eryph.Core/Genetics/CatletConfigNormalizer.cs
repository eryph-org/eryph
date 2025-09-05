using System;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using LanguageExt;
using LanguageExt.Common;

using static Eryph.ConfigModel.Validations;
using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

#nullable enable

public static class CatletConfigNormalizer
{
    public static Validation<Error, CatletConfig> Normalize(
        CatletConfig config) =>
        from name in Optional(config.Name)
            .Filter(notEmpty)
            .Map(CatletName.NewValidation)
            .Sequence()
        from parent in Optional(config.Parent)
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewValidation)
            .Sequence()
        let hostName = Optional(config.Hostname).Filter(notEmpty) | name.Map(n => n.Value)
        from projectName in Optional(config.Project).Filter(notEmpty).Match(
            Some: ProjectName.NewValidation,
            None: () => ProjectName.New(EryphConstants.DefaultProjectName))
        from dataStoreName in Optional(config.Store).Filter(notEmpty).Match(
            Some: DataStoreName.NewValidation,
            None: () => DataStoreName.New(EryphConstants.DefaultDataStoreName))
        from environmentName in Optional(config.Environment).Filter(notEmpty).Match(
            Some: EnvironmentName.NewValidation,
            None: () => EnvironmentName.New(EryphConstants.DefaultEnvironmentName))
        from storageIdentifier in Optional(config.Location)
            .Filter(notEmpty)
            .Map(StorageIdentifier.NewValidation)
            .Sequence()
        from drives in config.Drives.ToSeq().Map(Normalize).Sequence()
        from networks in config.Networks.ToSeq().Map(Normalize).ToSeq().Sequence()
        from _ in ValidateDistinct(
                networks,
                n => CatletNetworkAdapterName.NewValidation(n.AdapterName),
                "network adapter")
            .MapFail(e => Error.New("The network adapter names of the networks are not unique.", e))
        from networkAdapters in config.NetworkAdapters.ToSeq().Map(Normalize).Sequence()
        from allNetworkAdapters in AddMissingAdapters(networkAdapters, networks)
        from fodder in config.Fodder.ToSeq().Map(Normalize).Sequence()
        from variables in config.Variables.ToSeq().Map(Normalize).Sequence()
        select config.CloneWith(c =>
        {
            c.Name = name.Map(n => n.Value).IfNoneUnsafe((string?)null);
            c.Parent = parent.Map(p => p.Value).IfNoneUnsafe((string?)null);
            c.Hostname = hostName.IfNoneUnsafe((string?)null);
            c.Project = projectName.Value;
            c.Environment = environmentName.Value;
            c.Store = dataStoreName.Value;
            c.Location = storageIdentifier.Map(s => s.Value).IfNoneUnsafe((string?)null);
            c.Drives = drives.ToArray();
            c.Networks = networks.ToArray();
            c.NetworkAdapters = allNetworkAdapters.ToArray();
            c.Fodder = fodder.ToArray();
            c.Variables = variables.ToArray();
        });

    private static Validation<Error, CatletDriveConfig> Normalize(
        CatletDriveConfig config) =>
        from name in CatletDriveName.NewValidation(config.Name)
        from dataStoreName in Optional(config.Store).Filter(notEmpty).Match(
            Some: DataStoreName.NewValidation,
            None: () => DataStoreName.New(EryphConstants.DefaultDataStoreName))
        from storageIdentifier in Optional(config.Location)
            .Filter(notEmpty)
            .Map(StorageIdentifier.NewValidation)
            .Sequence()
        from geneSource in Optional(config.Source)
            .Filter(s => s.StartsWith("gene:", StringComparison.OrdinalIgnoreCase))
            .Map(GeneIdentifier.NewValidation)
            .Sequence()
        let source = geneSource.Map(g => g.Value) | Optional(config.Source).Filter(notEmpty)
        select config.CloneWith(c =>
        {
            c.Type = Optional(c.Type).IfNone(CatletDriveType.Vhd);
            c.Name = name.Value;
            c.Source = source.IfNoneUnsafe((string?)null);
            c.Store = dataStoreName.Value;
            c.Location = storageIdentifier.Map(s => s.Value).IfNoneUnsafe((string?)null);
        });

    private static Validation<Error, FodderConfig> Normalize(
        FodderConfig fodderConfig) =>
        from name in Optional(fodderConfig.Name)
            .Filter(notEmpty)
            .Map(FodderName.NewValidation)
            .Sequence()
        from source in Optional(fodderConfig.Source)
            .Filter(notEmpty)
            .Map(GeneIdentifier.NewValidation)
            .Sequence()
        from variables in fodderConfig.Variables.ToSeq().Map(Normalize).Sequence()
        select fodderConfig.CloneWith(c =>
        {
            c.Name = name.Map(n => n.Value).IfNoneUnsafe((string?)null);
            c.Source = source.Map(n => n.Value).IfNoneUnsafe((string?)null);
            c.Variables = variables.ToArray();
        });

    private static Validation<Error, VariableConfig> Normalize(
        VariableConfig config) =>
        from name in VariableName.NewValidation(config.Name)
        select config.CloneWith(c =>
        {
            c.Name = name.Value;
            c.Type = Optional(config.Type).IfNone(VariableType.String);
        });

    private static Validation<Error, CatletNetworkConfig> Normalize(
        int index,
        CatletNetworkConfig config) =>
        from name in EryphNetworkName.NewValidation(config.Name)
        from adapterName in Optional(config.AdapterName)
            .Filter(notEmpty)
            .Map(CatletNetworkAdapterName.NewValidation)
            .Sequence()
        from subnetV4 in Optional(config.SubnetV4)
            .Map(Normalize)
            .Sequence()
        from subnetV6 in Optional(config.SubnetV6)
            .Map(Normalize)
            .Sequence()
        select config.CloneWith(c =>
        {
            c.Name = name.Value;
            c.AdapterName = adapterName.Map(n => n.Value).IfNone($"eth{index}");
            c.SubnetV4 = subnetV4.IfNoneUnsafe((CatletSubnetConfig?)null);
            c.SubnetV6 = subnetV6.IfNoneUnsafe((CatletSubnetConfig?)null);
        });

    private static Validation<Error, CatletSubnetConfig> Normalize(
        CatletSubnetConfig config) =>
        from name in EryphSubnetName.NewValidation(config.Name)
        from poolName in Optional(config.IpPool)
            .Filter(notEmpty)
            .Map(EryphIpPoolName.NewValidation)
            .Sequence()
        select config.CloneWith(c =>
        {
            c.Name = name.Value;
            c.IpPool = poolName.Map(n => n.Value).IfNoneUnsafe((string?)null);
        });

    private static Validation<Error, CatletNetworkAdapterConfig> Normalize(
        CatletNetworkAdapterConfig config) =>
        from name in CatletNetworkAdapterName.NewValidation(config.Name)
        from macAddress in Optional(config.MacAddress)
            .Filter(notEmpty)
            .Map(EryphMacAddress.NewValidation)
            .Sequence()
        select config.CloneWith(c =>
        {
            c.Name = name.Value;
            c.MacAddress = macAddress.Map(m => m.Value).IfNoneUnsafe((string?)null);
        });

    private static Validation<Error, Seq<CatletNetworkAdapterConfig>> AddMissingAdapters(
        Seq<CatletNetworkAdapterConfig> networkAdapters,
        Seq<CatletNetworkConfig> networks) =>
        from _ in Success<Error, Unit>(unit)
        let adaptersByName = networkAdapters.Map(a => (a.Name, a)).ToHashMap()
        let adaptersForNetworks = networks
            .Map(n => adaptersByName.Find(n.AdapterName).Match(
                Some: identity,
                None: () => new CatletNetworkAdapterConfig { Name = n.AdapterName }))
        let missingAdapters = networkAdapters
            .Filter(a => !adaptersForNetworks.Any(afn => a.Name == afn.Name))
        select adaptersForNetworks.Concat(missingAdapters);

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

    private static CatletCpuConfig? Minimize(
        Option<CatletCpuConfig> config) =>
        config.Filter(c => c.Count.HasValue)
            .Map(c => c.Clone())
            .IfNoneUnsafe((CatletCpuConfig?)null);

    private static CatletMemoryConfig? Minimize(
        Option<CatletMemoryConfig> config) =>
        config.Filter(c => c.Startup.HasValue || c.Minimum.HasValue || c.Maximum.HasValue)
            .Map(c => c.Clone())
            .IfNoneUnsafe((CatletMemoryConfig?)null);

    private static FodderConfig[]? Minimize(
        Seq<FodderConfig> configs) =>
        configs.Match(
            Empty: () => null!,
            Seq: s => s.Map(Minimize).ToArray());

    private static FodderConfig Minimize(FodderConfig config) =>
        config.CloneWith(c => { c.Variables = Minimize(Seq(c.Variables)); });

    private static T[]? Minimize<T>(
        Seq<T> configs)
        where T : ICloneableConfig<T> =>
        configs.Match(
            Empty: () => null!,
            Seq: s => s.Map(c => c.Clone()).ToArray());
}
