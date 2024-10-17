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

    public static EitherAsync<Error, CatletConfig> Feed(
        CatletConfig catletConfig,
        HashMap<GeneIdentifier, GeneArchitecture> resolvedGenes,
        ILocalGenepoolReader genepoolReader) =>
        from allRemovedFodderKeys in catletConfig.Fodder.ToSeq()
            .Filter(f => f.Remove.GetValueOrDefault())
            .Map(f => FodderKey.Create(f.Name, f.Source))
            .Sequence()
            .ToAsync()
        from _ in allRemovedFodderKeys
            .Map(k => k.Source)
            .Somes()
            .Map(s => ValidateIsResolved(s, genepoolReader))
            .SequenceSerial()
        let removedSources = allRemovedFodderKeys
            .Filter(k => k.Name.IsNone)
            .Map(k => k.Source)
            .ToHashSet()
        let removedFodderKeys = allRemovedFodderKeys
            .Filter(k => k.Name.IsSome)
            .ToHashSet()
        let fodder = catletConfig.Fodder.ToSeq()
            .Filter(f => !f.Remove.GetValueOrDefault())
        from expandedFodder in fodder
            .Map(f => ExpandFodderConfig(f, resolvedGenes, genepoolReader))
            .SequenceSerial()
            .Map(l => l.Flatten())
        from expandedFodderWithKeys in expandedFodder
            .Map(FodderWithKey.Create)
            .Sequence()
            .ToAsync()
        let filteredFodder = expandedFodderWithKeys
            .Filter(fwk => !fwk.Key.Source.Map(s => removedSources.Contains(s)).IfNone(false))
            .Filter(fwk => !removedFodderKeys.Contains(fwk.Key))
        let mergedFodder = filteredFodder
            .ToLookup(fwk => fwk.Key, fwk => fwk.Config)
            .Map(g => g.Aggregate(CatletBreeding.MergeFodder))
        let fedConfig = catletConfig.CloneWith(c =>
        {
            c.Fodder = mergedFodder.ToArray();
        })
        select fedConfig;

    public static EitherAsync<Error, Seq<FodderConfig>> ExpandFodderConfig(
        FodderConfig fodderConfig,
        HashMap<GeneIdentifier, GeneArchitecture> resolvedGenes,
        ILocalGenepoolReader genepoolReader) =>
        from geneId in Optional(fodderConfig.Source)
            .Filter(notEmpty)
            .Filter(s => s.StartsWith("gene:"))
            .Map(GeneIdentifier.NewEither)
            .Sequence()
            .FilterT(id => id.GeneName != GeneName.New("catlet"))
            .ToAsync()
        from expanded in geneId.Match(
            Some: id => ExpandFodderConfigFromSource(fodderConfig, resolvedGenes, genepoolReader)
                .MapLeft(e => Error.New($"Could not expand the fodder gene '{id}'.", e)),
            None: () => Seq([fodderConfig]))
        select expanded;

    public static EitherAsync<Error, Seq<FodderConfig>> ExpandFodderConfigFromSource(
        FodderConfig fodderConfig,
        HashMap<GeneIdentifier, GeneArchitecture> resolvedGenes,
        ILocalGenepoolReader genepoolReader) =>
        from geneId in GeneIdentifier.NewEither(fodderConfig.Source).ToAsync()
        from _ in ValidateIsResolved(geneId, genepoolReader)
        from name in Optional(fodderConfig.Name)
            .Filter(notEmpty)
            .Map(FodderName.NewEither)
            .Sequence()
            .ToAsync()
        from architecture in resolvedGenes.Find(geneId)
            .ToEither(Error.New($"Could not find the architecture for gene '{geneId}'."))
            .ToAsync()
        from geneContent in genepoolReader.ReadGeneContent(GeneType.Fodder, architecture, geneId)
        from geneFodderConfig in Try(() =>
        {
            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(geneContent);
            return FodderGeneConfigDictionaryConverter.Convert(configDictionary);

        }).ToEither(Error.New).ToAsync()
        from geneFodderWithName in geneFodderConfig.Fodder.ToSeq()
            .Map(f => from n in FodderName.NewEither(f.Name)
                      select (Name: n, Config: f))
            .Sequence()
            .ToAsync()
        let filteredGeneFodder = geneFodderWithName
            .Filter(fwn => name.IsNone || fwn.Name == name)
            .Map(fwn => fwn.Config)
        from boundVariables in BindVariables(fodderConfig.Variables.ToSeq(), geneFodderConfig.Variables.ToSeq())
            .ToAsync()
        let boundFodder = filteredGeneFodder
            .Map(f => f.CloneWith(c =>
            {
                c.Source = geneId.Value;
                c.Variables = boundVariables.Map(vc => vc.Clone()).ToArray();
            }))
            .ToSeq()
        select boundFodder;

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

    private static EitherAsync<Error, Unit> ValidateIsResolved(
        GeneIdentifier geneId,
        ILocalGenepoolReader genepoolReader) =>
        from resolvedId in genepoolReader.GetGenesetReference(geneId.GeneSet)
            .MapLeft(e => Error.New($"Could not access gene '{geneId}' in the local genepool.", e))
        from __ in guard(resolvedId.IsNone,
            Error.New($"The gene '{geneId}' is an unresolved reference. This should not happen."))
        select unit;
}
