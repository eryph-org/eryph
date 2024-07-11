using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Variables;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Resources.Machines;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class CatletFeeding
{
    public static CatletConfig FeedSystemVariables(
        CatletConfig config,
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
                    Value = catletMetadata.VMId.ToString(),
                    Required = false,
                    Secret = false,
                },
            ];
        });

    public static Either<Error, CatletConfig> Feed(
        CatletConfig catletConfig,
        ILocalGenepoolReader genepoolReader) =>
        from expandedFodder in ExpandFodderConfigs(genepoolReader, catletConfig.Fodder.ToSeq())
        let fedConfig = catletConfig.CloneWith(c =>
        {
            c.Fodder = expandedFodder.ToArray();
        })
        select fedConfig;

    private static Either<Error, FodderConfig[]> ExpandFodderConfigs(
        ILocalGenepoolReader genepoolReader,
        Seq<FodderConfig> fodder) =>
        from toRemove in fodder.Filter(f => f.Remove.GetValueOrDefault() && notEmpty(f.Source))
            .Map(f =>
                from geneId in GeneIdentifier.NewEither(f.Source)
                from _ in ValidateIsResolved(geneId, genepoolReader)
                from metadata in PrepareMetadata(f.Clone())
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
                from _ in ValidateIsResolved(geneId, genepoolReader)
                from expandedConfig in ExpandFodderConfigFromSource(genepoolReader,
                    fodder.Clone(),
                    toRemove.Filter(x => x.Source == geneId))
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

    private static Either<Error, Unit> ValidateIsResolved(
        GeneIdentifier geneId,
        ILocalGenepoolReader genepoolReader) =>
        from resolvedId in genepoolReader.GetGenesetReference(geneId.GeneSet)
            .MapLeft(e => Error.New($"Could not access gene '{geneId}' in local genepool.", e))
        from __ in guard(resolvedId.IsNone,
            Error.New($"The gene '{geneId}' is an unresolved reference. This should not happen."))
        select unit;
}
