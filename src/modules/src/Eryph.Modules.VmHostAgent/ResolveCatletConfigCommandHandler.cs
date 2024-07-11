using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

using GeneSetMap = HashMap<GeneSetIdentifier, GeneSetIdentifier>;
using CatletMap = HashMap<GeneSetIdentifier, CatletConfig>;

/// <summary>
/// This command handler resolves the ancestors and referenced gene sets
/// of the included <see cref="ResolveCatletConfigCommand.Config"/>.
/// The handler returns a <see cref="ResolveCatletConfigCommandResponse"/>
/// with the resolved <see cref="AncestorInfo"/>s as well as the
/// <see cref="CatletConfig"/>s of all ancestors.
/// </summary>
/// <remarks>
/// <para>
/// This command handler performs all necessary resolving at once.
/// This way, we limit the number of messages which we need to exchange
/// with the VM host agent.
/// </para>
/// <para>
/// The handler ensures that each <see cref="GeneSetIdentifier"/> is resolved
/// exactly once. This both limits the amount of requests to the gene pool and
/// also ensures that a gene set reference is consistently resolved to the same
/// gene set.
/// </para>
/// </remarks>
[UsedImplicitly]
public class ResolveCatletConfigCommandHandler(
    IMessageContext messageContext,
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    IGeneProvider geneProvider)
    : IHandleMessages<OperationTask<ResolveCatletConfigCommand>>
{
    public Task Handle(OperationTask<ResolveCatletConfigCommand> message) =>
        Handle(message.Command, messageContext.GetCancellationToken())
            .FailOrComplete(messaging, message);

    private EitherAsync<Error, ResolveCatletConfigCommandResponse> Handle(
        ResolveCatletConfigCommand command,
        CancellationToken cancellationToken) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
        let genepoolReader = new LocalGenepoolReader(vmHostAgentConfig)
        from result in Handle(command, geneProvider, genepoolReader, cancellationToken)
        select result;

    public static EitherAsync<Error, ResolveCatletConfigCommandResponse> Handle(
        ResolveCatletConfigCommand command,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader,
        CancellationToken cancellationToken) =>
        from genesetsFromConfig in ResolveGeneSets(command.Config, GeneSetMap.Empty,
                geneProvider, cancellationToken)
            .MapLeft(e => Error.New("Could not resolve genes in the catlet config.", e))
        from result in ResolveParent(command.Config.Parent, genesetsFromConfig, new CatletMap(),
            Seq<AncestorInfo>(), geneProvider, genepoolReader, cancellationToken)
        select new ResolveCatletConfigCommandResponse()
        {
            ParentConfigs = result.ResolvedCatlets.ToDictionary(),
            ResolvedGeneSets = result.ResolvedGeneSets.ToDictionary(),
        };

    private static EitherAsync<Error, (GeneSetMap ResolvedGeneSets, CatletMap ResolvedCatlets)> ResolveParent(
        Option<string> parentId,
        GeneSetMap resolvedGeneSets,
        CatletMap resolvedCatlets,
        Seq<AncestorInfo> visitedAncestors,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader,
        CancellationToken cancellationToken) =>
        from validParentId in parentId.Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .MapLeft(e => Error.New("The parent ID is invalid.", e))
            .MapLeft(e => CreateError(visitedAncestors, e))
            .ToAsync()
        from result in validParentId.Match<EitherAsync<Error, (GeneSetMap ResolvedGeneSets, CatletMap ResolvedConfig)>>(
            Some: id =>
                from resolvedParent in ResolveParent(id, resolvedGeneSets, resolvedCatlets,
                    visitedAncestors, geneProvider, genepoolReader, cancellationToken)
                select (resolvedParent.ResolvedGeneSets, resolvedParent.ResolvedCatlets),
            None: () => (resolvedGeneSets, resolvedCatlets))
        select result;

    private static EitherAsync<Error, (GeneSetMap ResolvedGeneSets, CatletMap ResolvedCatlets)> ResolveParent(
        GeneSetIdentifier id,
        GeneSetMap resolvedGeneSets,
        CatletMap resolvedCatlets,
        Seq<AncestorInfo> visitedAncestors,
        IGeneProvider geneProvider,
        ILocalGenepoolReader genepoolReader,
        CancellationToken cancellationToken) =>
        from resolvedId in resolvedGeneSets.Find(id)
            .ToEitherAsync(Error.New($"Could not resolve parent ID '{id}'."))
            .MapLeft(e => CreateError(visitedAncestors, e))
        let updatedVisitedAncestors = visitedAncestors.Add(new AncestorInfo(id, resolvedId))
        from _ in CatletPedigree.ValidateAncestorChain(updatedVisitedAncestors)
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
            .ToAsync()
        from provideResult in geneProvider.ProvideGene(
            GeneType.Catlet,
            new GeneIdentifier(resolvedId, GeneName.New("catlet")),
            (s1, i) => Task.FromResult(unit),
            default)
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from a in guard(provideResult.RequestedGene == provideResult.ResolvedGene,
            Error.New("The resolved gene is different. This code must only be called with resolved IDs. "
                      + $"Requested: {provideResult.RequestedGene}; Resolved: {provideResult.ResolvedGene}"))
        from config in ReadCatletConfig(resolvedId, genepoolReader).ToAsync()
        from resolveResult in ResolveGeneSets(config, resolvedGeneSets, geneProvider, cancellationToken)
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from parentsResult in ResolveParent(config.Parent, resolveResult, resolvedCatlets,
            updatedVisitedAncestors, geneProvider, genepoolReader, cancellationToken)
        select (
            parentsResult.ResolvedGeneSets,
            parentsResult.ResolvedCatlets.Add(resolvedId, config));

    private static EitherAsync<Error, GeneSetMap> ResolveGeneSets(
        CatletConfig catletConfig,
        GeneSetMap resolvedGeneSets,
        IGeneProvider geneProvider,
        CancellationToken cancellationToken) =>
        from geneIds in CatletGeneCollecting.CollectGenes(catletConfig)
            .ToEither().ToAsync()
            .MapLeft(Error.Many)
        let geneSetIds = geneIds.Map(iwt => iwt.GeneIdentifier.GeneSet).Distinct()
        from resolved in geneSetIds.Fold<EitherAsync<Error, GeneSetMap>>(
            resolvedGeneSets,
            (state, geneSetId) => state.Bind(m => ResolveGeneSet(geneSetId, m, geneProvider, cancellationToken)))
        select resolved;

    private static EitherAsync<Error, GeneSetMap> ResolveGeneSet(
        GeneSetIdentifier geneSetId,
        GeneSetMap resolvedGeneSets,
        IGeneProvider geneProvider,
        CancellationToken cancellationToken) =>
        resolvedGeneSets.Find(geneSetId).Match(
            Some: _ => resolvedGeneSets,
            None: () =>
                from resolvedGeneSet in ResolveGeneSet(geneSetId, geneProvider, cancellationToken)
                select resolvedGeneSets.Add(geneSetId, resolvedGeneSet));

    private static EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(
        GeneSetIdentifier geneSetId,
        IGeneProvider geneProvider,
        CancellationToken cancellationToken) =>
        from resolved in geneProvider.ResolveGeneSet(geneSetId, cancellationToken)
            .MapLeft(e => Error.New($"Could not resolve the gene set tag '{geneSetId}'.", e))
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
            visitedAncestors.Match(
                    Empty: () => "Could not resolve genes in the catlet config.",
                    Seq: ancestors =>
                        "Could not resolve genes in the ancestor "
                        + string.Join(" -> ", "catlet".Cons(ancestors.Map(a => a.ToString())))
                        + "."),
            innerError);
}
