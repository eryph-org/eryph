using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Variables;
using Eryph.Core;
using Eryph.GenePool.Model;
using Eryph.Resources.Machines;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement
{
    public static class CatletConfigExtensions
    {
        public static CatletConfig AppendSystemVariables(
            this CatletConfig config,
            CatletMetadata catletMetadata) =>
            config.CloneWith(c =>
            {
                c.Variables =
                [
                    ..c.Variables ?? [],
                    new VariableConfig
                    {
                        Name = EryphConstants.SystemVariables.CatletId,
                        Type = VariableType.String,
                        Value = catletMetadata.MachineId.ToString(),
                        Required = false,
                        Secret = false,
                    },
                    new VariableConfig
                    {
                        Name = EryphConstants.SystemVariables.VmId,
                        Type = VariableType.String,
                        Value = catletMetadata.MachineId.ToString(),
                        Required = false,
                        Secret = false,
                    },
                ];
            });

        public static Either<Error,CatletConfig> BreedAndFeed(
            this CatletConfig machineConfig,
            ILocalGenepoolReader genepoolReader,
            Option<CatletConfig> optionalParentConfig)
        {
            var breedConfig = optionalParentConfig.Match(
                None: machineConfig,
                Some: parent => parent.Breed(machineConfig, machineConfig.Parent));

            var updatedConfig =
                from drives in breedConfig.Drives.ToSeq()
                    .Map(drive => ExpandDriveConfig(genepoolReader, drive))
                    .Sequence()
                    .Map(l => l.Flatten())
                from expandedFodder in ExpandFodderConfigs(genepoolReader, breedConfig.Fodder.ToSeq())
                let newConfig = breedConfig.Apply(c =>
                {
                    c.Drives = drives.ToArray();
                    c.Fodder = expandedFodder.ToArray();
                    return c;
                })
               select newConfig;

            return updatedConfig;
        }

        private static Either<Error, Seq<CatletDriveConfig>> ExpandDriveConfig(
            ILocalGenepoolReader genepoolReader, 
            CatletDriveConfig drive)
        {
            if (string.IsNullOrEmpty(drive.Source) || !drive.Source.StartsWith("gene:"))
                return Seq1(drive);

            return
                from geneIdentifier in GeneIdentifier.NewEither(drive.Source)
                from resolvedIdentifier in ResolveGeneIdentifier(genepoolReader, geneIdentifier)
                let newConfig = drive.Apply(c =>
                {
                    c.Source = resolvedIdentifier.Value;
                    return c;
                })
                select Seq1(newConfig);
        }

        private static Either<Error, FodderConfig[]> ExpandFodderConfigs(
            ILocalGenepoolReader genepoolReader,
            Seq<FodderConfig> fodder) =>
            from toRemove in fodder.Filter(f => f.Remove.GetValueOrDefault() && notEmpty(f.Source))
                .Map(f => from geneId in GeneIdentifier.NewEither(f.Source)
                          from resolvedGeneId in ResolveGeneIdentifier(genepoolReader, geneId)
                          let updatedFodder = f.CloneWith(c =>
                          {
                              c.Source = resolvedGeneId.Value;
                          })
                          from metadata in PrepareMetadata(updatedFodder)
                          select metadata)
                .Sequence()
            from expandedFodders in fodder
                .Filter(f => !f.Remove.GetValueOrDefault())
                .Map(f => ExpandFodderConfig(genepoolReader, f, toRemove))
                .Sequence()
            select expandedFodders.Flatten().ToArray();

        private static Either<Error, Seq<FodderConfig>> ExpandFodderConfig(
            ILocalGenepoolReader genepoolReader,
            FodderConfig fodder,
            Seq<FodderConfigWithMetadata> toRemove) =>
            from geneIdentifier in Optional(fodder.Source)
                .Filter(notEmpty)
                .Filter(s => s.StartsWith("gene:"))
                .Map(GeneIdentifier.NewEither)
                .Sequence()
                .FilterT(geneId => geneId.GeneName != GeneName.New("catlet"))
            from result in geneIdentifier.Match(
                Some: geneId =>
                    from resolvedIdentifier in ResolveGeneIdentifier(genepoolReader, geneId)
                    let newConfig = fodder.CloneWith(c =>
                    {
                        c.Source = resolvedIdentifier.Value;
                    })
                    from expandedConfig in ExpandFodderConfigFromSource(genepoolReader, newConfig,
                        toRemove.Filter(x => x.Source == resolvedIdentifier))
                    select expandedConfig
                        .Filter(x => !x.Remove.GetValueOrDefault())
                        .Map(f => f.CloneWith(r =>
                        {
                            r.Source = fodder.Source;
                        })),
                // fodder may be not a gene but may have to be requested to be removed as well
                None: () => fodder.Remove.GetValueOrDefault(false)
                    ? Seq<FodderConfig>()
                    : Seq1(fodder))
            select result;

        private static Either<Error, Seq<FodderConfig>> ExpandFodderConfigFromSource(
            ILocalGenepoolReader genepoolReader, 
            FodderConfig config, 
            Seq<FodderConfigWithMetadata> toRemove)
        {
            // if fodder is flagged to be removed and has no name specified, we can skip lookup of content
            if (config.Remove.GetValueOrDefault(false) && string.IsNullOrWhiteSpace(config.Name))
                return Seq<FodderConfig>();

            // When a fodder source is removed without a name, all fodder from that source should be removed,
            // and we can skip the lookup.
            if (toRemove.Any(r => r.Name == None))
                return Seq<FodderConfig>();

            return
                from geneIdentifier in GeneIdentifier.NewEither(config.Source ?? throw new InvalidDataException())
                from geneContent in genepoolReader.ReadGeneContent(GeneType.Fodder, geneIdentifier)
                from geneFodderConfig in Try(() =>
                {
                    var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(geneContent);
                    return FodderGeneConfigDictionaryConverter.Convert(configDictionary);
                    
                }).ToEither(Error.New)
                from childFodderWithMetadata in geneFodderConfig.Fodder.ToSeq()
                    .Map(PrepareMetadata)
                    .Sequence()
                from name in Optional(config.Name)
                    .Filter(notEmpty)
                    .Map(FodderName.NewEither)
                    .Sequence()
                let includedFodder = name.Match(
                    Some: n => childFodderWithMetadata.Filter(f => f.Name == n),
                    None: () => childFodderWithMetadata)
                let excludedFodder = childFodderWithMetadata
                    .Filter(f => toRemove.Any(r => r.Name == f.Name))
                from boundVariables in BindVariables(config.Variables.ToSeq(), geneFodderConfig.Variables.ToSeq())
                select includedFodder.Except(excludedFodder)
                    .Map(f => f.Config.CloneWith(fc =>
                    {
                        fc.Variables = boundVariables.Map(vc => vc.Clone()).ToArray();
                    }))
                    .ToSeq();
        }

        private static Either<Error, Seq<VariableConfig>> BindVariables(
            Seq<VariableConfig> variables,
            Seq<VariableConfig> geneVariables) =>
            from variablesWithNames in variables
                .Map(vc => from name in VariableName.NewEither(vc.Name)
                           select (name, vc))
                .Sequence()
                .Map(s => s.ToHashMap())
            from boundVariables in geneVariables
                .Map(geneVc => BindVariable(geneVc, variablesWithNames))
                .Sequence()
            select boundVariables;

        private static Either<Error, VariableConfig> BindVariable(
            VariableConfig geneVariable,
            HashMap<VariableName, VariableConfig> variables) =>
            from name in VariableName.NewEither(geneVariable.Name)
            select variables.Find(name).Match(
                Some: v => geneVariable.CloneWith(r =>
                {
                    r.Value = v.Value ?? geneVariable.Value;
                    r.Secret = v.Secret | geneVariable.Secret;
                }),
                None: geneVariable.Clone());


        private static Either<Error, FodderConfigWithMetadata> PrepareMetadata(FodderConfig fodder) =>
            from geneId in Optional(fodder.Source).Filter(notEmpty)
                .Map(GeneIdentifier.NewEither)
                .Sequence()
            from fodderName in Optional(fodder.Name).Filter(notEmpty)
                .Map(FodderName.NewEither)
                .Sequence()
            select new FodderConfigWithMetadata(geneId, fodderName, fodder);
        

        private sealed record FodderConfigWithMetadata(
            Option<GeneIdentifier> Source,
            Option<FodderName> Name,
            FodderConfig Config);

        private static Either<Error, GeneIdentifier> ResolveGeneIdentifier(
            ILocalGenepoolReader genepoolReader,
            GeneIdentifier identifier)
        {

            var processedReferences = new List<string>();
            var startIdentifier = identifier;

            do
            {
                var genesetName = identifier.GeneSet.Value;

                if (processedReferences.Contains(genesetName))
                {
                    var referenceStack = string.Join(" -> ", processedReferences);
                    throw new InvalidDataException(
                        $"Circular reference detected in geneset '{startIdentifier.Value}': {referenceStack}.");
                }

                processedReferences.Add(genesetName);

                // found no way to make it work functional, so we have to extract the value from the option
                // to see if it is a reference or not
                var res = genepoolReader.GetGenesetReference(identifier.GeneSet).Map(o => o.Map(s =>
                {
                    identifier = new GeneIdentifier(s, identifier.GeneName);
                    return true;
                }).IfNone(false));

                if(res.IsLeft)
                    return res.Map(_ => identifier);

                var isReference = res.RightAsEnumerable().FirstOrDefault();
                if(isReference) continue;

                return identifier;
            } while (true);
        }
    }
}