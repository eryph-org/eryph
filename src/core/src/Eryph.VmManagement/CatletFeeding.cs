using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Variables;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Modules.GenePool.Genetics;
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
        FeedSystemVariables(
            config,
            catletMetadata.MachineId.ToString(),
            catletMetadata.VMId.ToString());

    public static CatletConfig FeedSystemVariables(
        CatletConfig config,
        string catletId,
        string vmId) =>
        config.CloneWith(c =>
        {
            c.Variables =
            [
                ..c.Variables ?? [],
                new VariableConfig
                {
                    Name = EryphConstants.SystemVariables.CatletId,
                    Type = VariableType.String,
                    Value = catletId,
                    Required = false,
                    Secret = false,
                },
                new VariableConfig
                {
                    Name = EryphConstants.SystemVariables.VmId,
                    Type = VariableType.String,
                    Value = vmId,
                    Required = false,
                    Secret = false,
                },
            ];
        });

    public static EitherAsync<Error, CatletConfig> Feed(
        CatletConfig catletConfig,
        HashMap<UniqueGeneIdentifier, GeneHash> resolvedGenes,
        IGenePoolReader genepoolReader) =>
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
        HashMap<UniqueGeneIdentifier, GeneHash> resolvedGenes,
        IGenePoolReader genepoolReader) =>
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
        HashMap<UniqueGeneIdentifier, GeneHash> resolvedGenes,
        IGenePoolReader genepoolReader) =>
        from geneId in GeneIdentifier.NewEither(fodderConfig.Source).ToAsync()
        from _ in ValidateIsResolved(geneId, genepoolReader)
        from name in Optional(fodderConfig.Name)
            .Filter(notEmpty)
            .Map(FodderName.NewEither)
            .Sequence()
            .ToAsync()
        from resolvedGene in resolvedGenes
            .Find(g => g.Key.GeneType == GeneType.Fodder && g.Key.Id == geneId)
            .ToEither(Error.New($"The gene '{geneId}' has not been correctly resolved. This should not happen."))
            .ToAsync()
        let uniqueGeneId = resolvedGene.Key
        let geneHash = resolvedGene.Value
        from geneContent in genepoolReader.GetGeneContent(uniqueGeneId, geneHash, CancellationToken.None)
        from geneFodderConfig in Try(() => FodderGeneConfigJsonSerializer.Deserialize(geneContent))
            .ToEither(Error.New).ToAsync()
        from geneFodderWithName in geneFodderConfig.Fodder.ToSeq()
            .Map(f => from n in FodderName.NewEither(f.Name)
                      select (Name: n, Config: f))
            .Sequence()
            .ToAsync()
        from filteredGeneFodderWithName in name.Match(
            Some: n => from food in geneFodderWithName
                          .Find(fwn => fwn.Name == n)
                          .ToEither(Error.New($"The food '{n}' does not exist in the gene '{uniqueGeneId.Id} ({uniqueGeneId.Architecture})'."))
                          .ToAsync()
                       select Seq1(food),
            None: () => geneFodderWithName)
        from boundVariables in BindVariables(fodderConfig.Variables.ToSeq(), geneFodderConfig.Variables.ToSeq())
            .ToAsync()
        let boundFodder = filteredGeneFodderWithName
            .Map(fwn => fwn.Config)
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
        from geneVariableNames in geneVariables
            .Map(v => VariableName.NewEither(v.Name))
            .Sequence()
        let geneVariableNamesSet = toHashSet(geneVariableNames)
        from _ in variablesWithNames
            .Filter((n, _) => !geneVariableNamesSet.Contains(n))
            .HeadOrNone().Match<Either<Error, Unit>>(
                Some: v => Error.New($"Found a binding for the variable '{v.Value.Name}' but the variable is not defined in the fodder gene."),
                None: () => unit)
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
        IGenePoolReader genepoolReader) =>
        from resolvedId in genepoolReader.GetReferencedGeneSet(geneId.GeneSet, CancellationToken.None)
            .MapLeft(e => Error.New($"Could not access gene '{geneId}' in the local genepool.", e))
        from __ in guard(resolvedId.IsNone,
            Error.New($"The gene '{geneId}' is an unresolved reference. This should not happen."))
        select unit;
}
