using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using Eryph.Genetics;
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
internal class ResolveCatletConfigCommandHandler(
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
        from genesetsFromConfig in ResolveGeneSets(command.Config, GeneSetMap.Empty, geneProvider)
        from result in ResolveParents(command.Config, genesetsFromConfig, Seq<GeneSetIdentifier>(), geneProvider,
            genepoolReader)
        select new ResolveCatletConfigCommandResponse()
        {
            ParentConfigs = result.resolvedCatlets.ToList(),
            ResolvedGeneSets = result.resolvedGeneSets.ToList(),
        };

    private static EitherAsync<Error, (GeneSetMap resolvedGeneSets, CatletMap resolvedCatlets)> ResolveParents(
        CatletConfig catletConfig,
        GeneSetMap resolvedGeneSets,
        Seq<GeneSetIdentifier> visitedGeneSets,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader) =>
        from resolved in ResolveParent(catletConfig.Parent, resolvedGeneSets, visitedGeneSets, geneProvider, genepoolReader)
            .MapLeft(e => Error.New(
              $"Could not resolve the parent of {string.Join(" -> ", "catlet".Cons(visitedGeneSets.Map(id => id.Value)))}", e))
        from result in resolved.ResolvedConfig.Match<EitherAsync<Error, (GeneSetMap resolvedGeneSets, CatletMap resolvedCatlets)>>(
                Some: cwi =>
                    from parents in ResolveParents(cwi.Config, resolved.ResolvedGeneSets, visitedGeneSets.Add(cwi.Id),
                        geneProvider, genepoolReader)
                    select (parents.resolvedGeneSets, parents.resolvedCatlets.Add(cwi.Id, cwi.Config)),
                None: () => (resolved.ResolvedGeneSets, new CatletMap()))
        select result;

    private static EitherAsync<Error, (GeneSetMap ResolvedGeneSets, Option<ConfigWithId> ResolvedConfig)> ResolveParent(
        Option<string> parentId,
        GeneSetMap resolvedGeneSets,
        Seq<GeneSetIdentifier> visitedGeneSets,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader) =>
        from validParentId in parentId.Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Map(r => r.MapLeft(e => Error.New($"The parent ID '{parentId}' is invalid.", e)).ToAsync())
            .Sequence()
        from result in validParentId.Match<EitherAsync<Error, (GeneSetMap ResolvedGeneSets, Option<ConfigWithId> ResolvedConfig)>>(
            Some: id =>
                from resolvedParent in ResolveParent(id, resolvedGeneSets, visitedGeneSets, geneProvider, genepoolReader)
                select (resolvedParent.ResolvedGeneSets, Some(resolvedParent.ResolvedConfig)),
            None: () => (resolvedGeneSets, Option<ConfigWithId>.None))
        select result;

    private static EitherAsync<Error, (GeneSetMap ResolvedGeneSets, ConfigWithId ResolvedConfig)> ResolveParent(
        GeneSetIdentifier parentId,
        GeneSetMap resolvedGeneSets,
        Seq<GeneSetIdentifier> visitedGeneSets,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader) =>
        from _ in guardnot(visitedGeneSets.Contains(parentId),
                Error.New("Circular reference detected: "
                          + string.Join(" -> ", visitedGeneSets.Add(parentId))))
            .ToEitherAsync()
        from __ in guardnot(visitedGeneSets.Count >= EryphConstants.Limits.MaxCatletAncestors,
                Error.New("The reference chain is too long: "
                          + string.Join(" -> ", visitedGeneSets.Add(parentId))))
            .ToEitherAsync()
        from resolvedParentId in resolvedGeneSets.Find(parentId)
            .ToEitherAsync(Error.New($"Could not resolve parent ID '{parentId}'."))
        from provideResult in geneProvider.ProvideGene(
            GeneType.Catlet,
            new GeneIdentifier(resolvedParentId, GeneName.New("catlet")),
            (s1, i) => Task.FromResult(unit),
            default)
        from a in guard(provideResult.RequestedGene == provideResult.ResolvedGene,
            Error.New("The resolved gene is different. This code must only be called with resolved IDs. "
                      + $"Requested: {provideResult.RequestedGene}; Resolved: {provideResult.ResolvedGene}"))
        from parentConfig in Eryph.VmManagement.CatletGeneResolving.ReadCatletConfig(resolvedParentId, genepoolReader).ToAsync()
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
}
