using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

using GeneSetMap = HashMap<GeneSetIdentifier, GeneSetIdentifier>;
using CatletMap = HashMap<GeneSetIdentifier, CatletConfig>;

[UsedImplicitly]
public class ResolveCatletConfigCommandHandler(
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    IGeneProvider geneProvider)
    : IHandleMessages<OperationTask<ResolveCatletConfigCommand>>
{
    public Task Handle(OperationTask<ResolveCatletConfigCommand> message) =>
        Handle(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, ResolveCatletConfigCommandResponse> Handle(
        ResolveCatletConfigCommand command) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genepoolReader = new LocalGenepoolReader(vmHostAgentConfig)
        from result in Handle(command, geneProvider, genepoolReader)
        select result;

    public static EitherAsync<Error, ResolveCatletConfigCommandResponse> Handle(
        ResolveCatletConfigCommand command,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader) =>
        from genesetsFromConfig in ResolveGeneSets(command.Config, GeneSetMap.Empty, geneProvider)
        from result in ResolveParents(command.Config, genesetsFromConfig, Seq<AncestorInfo>(), geneProvider,
            genepoolReader)
        select new ResolveCatletConfigCommandResponse()
        {
            ParentConfigs = result.resolvedCatlets.ToList(),
            ResolvedGeneSets = result.resolvedGeneSets.ToList(),
        };

    private static EitherAsync<Error, (GeneSetMap resolvedGeneSets, CatletMap resolvedCatlets)> ResolveParents(
        CatletConfig catletConfig,
        GeneSetMap resolvedGeneSets,
        Seq<AncestorInfo> visitedAncestors,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader) =>
        from resolved in ResolveParent(catletConfig.Parent, resolvedGeneSets, visitedAncestors, geneProvider, genepoolReader)
            .MapLeft(e => CreateError(visitedAncestors, e))
        from result in resolved.ResolvedConfig.Match<EitherAsync<Error, (GeneSetMap resolvedGeneSets, CatletMap resolvedCatlets)>>(
                Some: cwi =>
                    from parents in ResolveParents(cwi.Config, resolved.ResolvedGeneSets,
                        visitedAncestors.Add(new AncestorInfo(cwi.Id, cwi.Id)),
                        geneProvider, genepoolReader)
                    select (parents.resolvedGeneSets, parents.resolvedCatlets.Add(cwi.Id, cwi.Config)),
                None: () => (resolved.ResolvedGeneSets, new CatletMap()))
        select result;

    private static EitherAsync<Error, (GeneSetMap ResolvedGeneSets, Option<ConfigWithId> ResolvedConfig)> ResolveParent(
        Option<string> parentId,
        GeneSetMap resolvedGeneSets,
        Seq<AncestorInfo> visitedAncestors,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader) =>
        from validParentId in parentId.Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .MapLeft(e => Error.New("The parent ID is invalid.", e))
            .MapLeft(e => CreateError(visitedAncestors, e))
            .ToAsync()
        from result in validParentId.Match<EitherAsync<Error, (GeneSetMap ResolvedGeneSets, Option<ConfigWithId> ResolvedConfig)>>(
            Some: id =>
                from resolvedParent in ResolveParent(id, resolvedGeneSets, visitedAncestors, geneProvider, genepoolReader)
                select (resolvedParent.ResolvedGeneSets, Some(resolvedParent.ResolvedConfig)),
            None: () => (resolvedGeneSets, Option<ConfigWithId>.None))
        select result;

    private static EitherAsync<Error, (GeneSetMap ResolvedGeneSets, ConfigWithId ResolvedConfig)> ResolveParent(
        GeneSetIdentifier parentId,
        GeneSetMap resolvedGeneSets,
        Seq<AncestorInfo> visitedAncestors,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader) =>
        from resolvedParentId in resolvedGeneSets.Find(parentId)
            .ToEitherAsync(Error.New($"Could not resolve parent ID '{parentId}'."))
            .MapLeft(e => CreateError(visitedAncestors, e))
        let updatedVisitedAncestors = visitedAncestors.Add(new AncestorInfo(parentId, resolvedParentId))
        from _ in CatletPedigree.ValidateAncestorChain(updatedVisitedAncestors)
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
            .ToAsync()
        from provideResult in geneProvider.ProvideGene(
            GeneType.Catlet,
            new GeneIdentifier(resolvedParentId, GeneName.New("catlet")),
            (s1, i) => Task.FromResult(unit),
            default)
        from a in guard(provideResult.RequestedGene == provideResult.ResolvedGene,
            Error.New("The resolved gene is different. This code must only be called with resolved IDs. "
                      + $"Requested: {provideResult.RequestedGene}; Resolved: {provideResult.ResolvedGene}"))
        from parentConfig in ReadCatletConfig(resolvedParentId, genepoolReader).ToAsync()
        from result in ResolveGeneSets(parentConfig, resolvedGeneSets, geneProvider)
        select (result, new ConfigWithId(parentConfig, resolvedParentId));

    private readonly record struct ConfigWithId(CatletConfig Config, GeneSetIdentifier Id);

    private static EitherAsync<Error, GeneSetMap> ResolveGeneSets(
        CatletConfig catletConfig,
        GeneSetMap resolvedGeneSets,
        IGeneProvider geneProvider) =>
        from geneIds in CatletGeneCollecting.CollectGenes(catletConfig)
            .ToEither().ToAsync()
            .MapLeft(Error.Many)
        let geneSetIds = geneIds.Map(iwt => iwt.GeneIdentifier.GeneSet).Distinct()
        from resolved in geneSetIds.Fold<EitherAsync<Error, GeneSetMap>>(
            resolvedGeneSets, (state, geneSetId) => state.Bind(m => ResolveGeneSet(geneSetId, m, geneProvider)))
        select resolved;

    private static EitherAsync<Error, GeneSetMap> ResolveGeneSet(
        GeneSetIdentifier geneSetId,
        GeneSetMap resolvedGeneSets,
        IGeneProvider geneProvider) =>
        resolvedGeneSets.Find(geneSetId).Match(
            Some: _ => resolvedGeneSets,
            None: () =>
                from resolvedGeneSet in ResolveGeneSet(geneSetId, geneProvider)
                select resolvedGeneSets.Add(geneSetId, resolvedGeneSet));

    private static EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(
        GeneSetIdentifier geneSetId,
        IGeneProvider geneProvider) =>
        from resolved in geneProvider.ResolveGeneSet(geneSetId, (_, _) => Task.FromResult(unit), default)
        select resolved;


    public static Either<Error, CatletConfig> ReadCatletConfig(
        GeneSetIdentifier geneSetId,
        ILocalGenepoolReader genepoolReader) =>
        from json in genepoolReader.ReadGeneContent(
            GeneType.Catlet, new GeneIdentifier(geneSetId, GeneName.New("catlet")))
        from config in Try(() =>
        {
            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(json);
            return CatletConfigDictionaryConverter.Convert(configDictionary);
        }).ToEither(ex => Error.New($"Could not deserialize catlet config '{geneSetId}'", Error.New(ex)))
        select config;

    private static Error CreateError(
        Seq<AncestorInfo> visitedAncestors,
        Error innerError) =>
        Error.New(
            "Could not resolve ancestor in the pedigree "
            + string.Join(" -> ", "catlet".Cons(visitedAncestors.Map(a => a.ToString()))),
            innerError);
}
