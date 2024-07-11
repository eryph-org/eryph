using System;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public static class CatletBreeding
{
    /// <summary>
    /// Breeds the <paramref name="parentConfig"/> with the <paramref name="childConfig"/>.
    /// </summary>
    public static Either<Error, CatletConfig> Breed(
        CatletConfig parentConfig,
        CatletConfig childConfig) =>
        from cpu in BreedCpu(parentConfig.Cpu, childConfig.Cpu)
        from memory in BreedMemory(parentConfig.Memory, childConfig.Memory)
        from drives in BreedDrives(Seq(parentConfig.Drives), Seq(childConfig.Drives))
        from networkAdapters in BreedNetworkAdapters(Seq(parentConfig.NetworkAdapters), Seq(childConfig.NetworkAdapters))
        from networks in BreedNetworks(Seq(parentConfig.Networks), Seq(childConfig.Networks))
        from variables in BreedVariables(Seq(parentConfig.Variables), Seq(childConfig.Variables))
        from fodder in BreedFodder(Seq(parentConfig.Fodder), Seq(childConfig.Fodder))
        from capabilities in BreedCapabilities(Seq(parentConfig.Capabilities), Seq(childConfig.Capabilities))
        select new CatletConfig()
        {
            // Basic catlet configuration like name and placement information
            // is not inherited from parent
            Name = childConfig.Name,
            Parent = childConfig.Parent,
            Version = childConfig.Version,
            Project = childConfig.Project,
            Location = childConfig.Location,
            Environment = childConfig.Environment,
            Store = childConfig.Store,
            Hostname = childConfig.Hostname,

            // Bred configuration
            Capabilities = capabilities.ToArray(),
            Cpu = cpu.IfNoneUnsafe((CatletCpuConfig)null),
            Drives = drives.ToArray(),
            Fodder = fodder.ToArray(),
            Memory = memory.IfNoneUnsafe((CatletMemoryConfig)null),
            Networks = networks.ToArray(),
            NetworkAdapters = networkAdapters.ToArray(),
            Variables = variables.ToArray(),
        };

    public static Either<Error, Option<CatletCpuConfig>> BreedCpu(
        Option<CatletCpuConfig> parentConfig,
        Option<CatletCpuConfig> childConfig) =>
        BreedOptional(parentConfig, childConfig,
            (parent, child) => new CatletCpuConfig()
            {
                Count = child.Count ?? parent.Count
            });

    public static Either<Error, Option<CatletMemoryConfig>> BreedMemory(
        Option<CatletMemoryConfig> parentConfig,
        Option<CatletMemoryConfig> childConfig) =>
        BreedOptional(parentConfig, childConfig,
            (parent, child) => new CatletMemoryConfig()
            {
                Minimum = child.Minimum ?? parent.Minimum,
                Maximum = child.Maximum ?? parent.Maximum,
                Startup = child.Startup ?? parent.Startup,
            });

    public static Either<Error, Seq<CatletDriveConfig>> BreedDrives(
        Seq<CatletDriveConfig> parentConfigs,
        Seq<CatletDriveConfig> childConfigs) =>
        BreedMutateable(parentConfigs, childConfigs,
            CatletDriveName.NewEither,
            (parent, child) =>
                from _ in Right<Error, Unit>(unit)
                let diskType = (Optional(child.Type) | Optional(parent.Type)).IfNone(CatletDriveType.VHD)
                let source = Optional(child.Source) | Optional(parent.Source)
                let size = Optional(child.Size).Filter(s => s != 0) | Optional(parent.Size)
                let location = Optional(child.Location).Filter(notEmpty) | Optional(parent.Location)
                let store = Optional(child.Store).Filter(notEmpty) | Optional(parent.Store)
                from __ in guardnot(
                        source.Bind(GeneIdentifier.NewOption).IsSome && diskType != CatletDriveType.VHD,
                        Error.New("The drive must be a plain VHD when using a gene pool source."))
                    .ToEither()
                select new CatletDriveConfig()
                {
                    Name = child.Name,
                    Mutation = child.Mutation,

                    Type = diskType,
                    Location = location.IfNoneUnsafe((string)null),
                    Size = size.Map(s => (int?)s).IfNoneUnsafe((int?)null),
                    Store = store.IfNoneUnsafe((string)null),
                    Source = source.IfNoneUnsafe((string)null),
                });

    public static Either<Error, Seq<CatletNetworkAdapterConfig>> BreedNetworkAdapters(
        Seq<CatletNetworkAdapterConfig> parentConfigs,
        Seq<CatletNetworkAdapterConfig> childConfigs) =>
        BreedMutateable(parentConfigs, childConfigs,
            CatletNetworkAdapterName.NewEither,
            (parent, child) => new CatletNetworkAdapterConfig()
            {
                Name = child.Name,
                Mutation = child.Mutation,

                MacAddress = child.MacAddress ?? parent.MacAddress,
            });

    public static Either<Error, Seq<CatletNetworkConfig>> BreedNetworks(
        Seq<CatletNetworkConfig> parentConfigs,
        Seq<CatletNetworkConfig> childConfigs) =>
        BreedMutateable(parentConfigs, childConfigs,
            EryphNetworkName.NewEither,
            (parent, child) => new CatletNetworkConfig()
            {
                Name = child.Name,
                Mutation = child.Mutation,

                AdapterName = child.AdapterName ?? parent.AdapterName,
                SubnetV4 = child.SubnetV4?.Clone() ?? parent.SubnetV4?.Clone(),
                SubnetV6 = child.SubnetV6?.Clone() ?? parent.SubnetV6?.Clone(),
            });

    public static Either<Error, Seq<VariableConfig>> BreedVariables(
        Seq<VariableConfig> parentDrives,
        Seq<VariableConfig> childDrives) =>
        from parentsWithNames in parentDrives
            .Map(c => from validName in VariableName.NewEither(c.Name)
                select new ConfigWithName<VariableConfig, VariableName>(validName, c))
            .Sequence()
        from childrenWithNames in childDrives
            .Map(c => from validName in VariableName.NewEither(c.Name)
                select new ConfigWithName<VariableConfig, VariableName>(validName, c))
            .Sequence()
        // Validate that there are no duplicate variable names as it would break the breeding.
        // Normally, these cases should have been caught by the validation earlier.
        from _ in ValidateDistinct(parentsWithNames)
            .MapLeft(e => Error.New("Some variable names in the parent config are not unique.", e)) 
        from __ in ValidateDistinct(childrenWithNames)
            .MapLeft(e => Error.New("Some variable names in the child config are not unique.", e))
        let childrenMap = childrenWithNames.Map(v => (v.Name, v.Config)).ToHashMap()
        let merged = parentsWithNames
            .Map(p => childrenMap.Find(p.Name).Match(
                // Merging a variable config is potentially problematic, e.g. the merge could
                // remove the secret flag without the user realizing the variable's value is
                // sensitive. Hence, a child variable always completely replaces the parent variable.
                Some: c => p with { Config = c.Clone() },
                None: () => p with { Config = p.Config.Clone() }))
        let mergedMap = merged.Map(v => (v.Name, v.Config)).ToHashMap()
        let additional = childrenWithNames
            .Filter(c => mergedMap.Find(c.Name).IsNone)
            .Map(c => c with { Config = c.Config.Clone() })
        select merged.Concat(additional).Map(c => c.Config);

    private static Either<Error, Seq<FodderConfig>> BreedFodder(
        Seq<FodderConfig> parentConfigs,
        Seq<FodderConfig> childConfigs) =>
        from parentsWithKeys in parentConfigs
            .Map(FodderWithKey.Create)
            .Sequence()
        from childrenWithKeys in childConfigs
            .Map(FodderWithKey.Create)
            .Sequence()
        // Validate that there is no duplicate fodder as it would break the breeding.
        // Normally, these cases should have been caught by the validation earlier.
        from _ in ValidateDistinct(parentsWithKeys)
            .MapLeft(e => Error.New("Some fodder in the parent config is not unique.", e))
        from __ in ValidateDistinct(childrenWithKeys)
            .MapLeft(e => Error.New("Some fodder in the child config is not unique.", e))
        from ___ in FodderConfigValidations.ValidateNoMultipleTagsForGeneSet(
            parentsWithKeys.Append(childrenWithKeys).Map(fwk => fwk.Key.Source).Somes())
            .ToEither()
            .MapLeft(errors => Error.New(
                "The parent and child config use one or more gene sets with multiple tags.",
                Error.Many(errors)))
        let childrenMap = childrenWithKeys.Map(v => (v.Key, v.Config)).ToHashMap()
        let merged = parentsWithKeys
            .Filter(p => p.Key.Source.IsSome || !p.Config.Remove.GetValueOrDefault())
            .Filter(p => p.Key.Source.IsSome || childrenMap.Find(p.Key).Filter(c => c.Remove.GetValueOrDefault()).IsNone)
            .Map(p => p with
            {
                Config = childrenMap.Find(p.Key).Match(
                    Some: c => MergeFodder(p.Config, c),
                    None: () => p.Config.Clone())
            })
        let mergedMap = merged.Map(v => (v.Key, v.Config)).ToHashMap()
        let additional = childrenWithKeys
            .Filter(c => c.Key.Source.IsSome || !c.Config.Remove.GetValueOrDefault())
            .Filter(c => mergedMap.Find(c.Key).IsNone)
            .Map(c => c with { Config = c.Config.Clone() })
        select merged.Concat(additional).Map(c => c.Config);

    private static FodderConfig MergeFodder(FodderConfig parent, FodderConfig child) => new()
    {
        // Name and source should be the same for parent and child.
        // Otherwise, we would not merge
        Name = child.Name,
        Source = child.Source,

        Content = child.Content ?? parent.Content,
        FileName = child.FileName ?? parent.FileName,
        Remove = child.Remove ?? parent.Remove,
        Secret = child.Secret ?? parent.Secret,
        Type = child.Type ?? parent.Type,

        // A parameterized fodder content is only useful with its corresponding
        // variables. Hence, we take the variables from the fodder config which
        // provides the content or the source.
        Variables = child.Content is not null || child.Source is not null
            ? child.Variables?.Select(x => x.Clone()).ToArray()
            : parent.Variables?.Select(x => x.Clone()).ToArray(),
    };

    private static Either<Error, Unit> ValidateDistinct(
        Seq<FodderWithKey> fodderWithKeys) =>
        Validations.ValidateDistinct<FodderWithKey, FodderKey>(
                fodderWithKeys, fwk => fwk.Key, "fodder")
            .ToEither()
            .MapLeft(Error.Many);

    private sealed record FodderWithKey(FodderKey Key, FodderConfig Config)
    {
        public static Either<Error, FodderWithKey> Create(FodderConfig config) =>
            from fodderKey in FodderKey.Create(config.Name, config.Source)
            select new FodderWithKey(fodderKey, config);
    };

    public static Either<Error, Seq<CatletCapabilityConfig>> BreedCapabilities(
        Seq<CatletCapabilityConfig> parentConfigs,
        Seq<CatletCapabilityConfig> childConfigs) =>
        BreedMutateable(parentConfigs, childConfigs,
            CatletCapabilityName.NewEither,
            (parent, child) => new CatletCapabilityConfig()
            {
                Name = child.Name,
                Details = child.Details?.ToArray() ?? parent.Details?.ToArray(),
            });

    private static Either<Error, Option<TConfig>> BreedOptional<TConfig>(
        Option<TConfig> parentConfig,
        Option<TConfig> childConfig,
        Func<TConfig,TConfig,TConfig> breed)
        where TConfig : ICloneableConfig<TConfig> =>
        parentConfig.Match(
            Some: parent => childConfig.Match(
                Some: child => breed(parent, child),
                None: parent.Clone()),
            None: childConfig.Map(c => c.Clone()));

    private static Either<Error, Seq<TConfig>> BreedMutateable<TConfig, TName>(
        Seq<TConfig> parentConfigs,
        Seq<TConfig> childConfigs,
        Func<string, Either<Error, TName>> parseName,
        Func<TConfig, TConfig, Either<Error, TConfig>> merge)
        where TConfig : IMutateableConfig<TConfig>
        where TName : EryphName<TName> =>
        from parentsWithNames in parentConfigs
            .Map(c => from name in parseName(c.Name)
                      select new ConfigWithName<TConfig, TName>(name, c))
            .Sequence()
        from childrenWithNames in childConfigs
            .Map(c => from name in parseName(c.Name)
                      select new ConfigWithName<TConfig, TName>(name, c))
            .Sequence()
        // Validate that there are no duplicate names as it would break the breeding.
        // Normally, these cases should have been caught by the validation earlier.
        from _ in ValidateDistinct(parentsWithNames)
            .MapLeft(e => Error.New("Some names in the parent config are not unique.", e)) 
        from __ in ValidateDistinct(childrenWithNames)
            .MapLeft(e => Error.New("Some names in the child config are not unique.", e))
        let childrenMap = childrenWithNames.Map(v => (v.Name, v.Config)).ToHashMap()
        from merged in parentsWithNames
            .Filter(p => p.Config.Mutation != MutationType.Remove)
            .Filter(p => childrenMap.Find(p.Name).Filter(c => c.Mutation is MutationType.Remove).IsNone)
            .Map(p => childrenMap.Find(p.Name).Match(
                    Some: c => Optional(c.Mutation).Filter(m => m == MutationType.Overwrite).Match(
                            Some: _ => c.Clone(),
                            None: () => merge(p.Config, c)),
                    None: () => p.Config.Clone())
                .Map(c => p with { Config = c }))
            .Sequence()
        let mergedMap = merged.Map(v => (v.Name, v.Config)).ToHashMap()
        let additional = childrenWithNames
            .Filter(c => c.Config.Mutation != MutationType.Remove)
            .Filter(c => mergedMap.Find(c.Name).IsNone)
            .Map(c => c with { Config = c.Config.Clone() })
        select merged.Concat(additional).Map(c => c.Config);

    private sealed record ConfigWithName<TConfig, TName>(TName Name, TConfig Config)
        where TName : EryphName<TName>;

    private static Either<Error, Unit> ValidateDistinct<TConfig, TName>(
        Seq<ConfigWithName<TConfig, TName>> configsWithNames)
        where TName : EryphName<TName> =>
        Validations.ValidateDistinct<ConfigWithName<TConfig, TName>, TName>(
                configsWithNames, cwn => cwn.Name, nameof(TName))
            .ToEither()
            .MapLeft(Error.Many);
}
