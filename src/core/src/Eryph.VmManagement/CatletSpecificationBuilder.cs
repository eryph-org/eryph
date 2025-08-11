using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using System.Linq;
using System.Threading;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

using ArchitectureMap = HashMap<GeneIdentifierWithType, Architecture>;
using CatletMap = HashMap<GeneSetIdentifier, CatletConfig>;
using GeneSetMap = HashMap<GeneSetIdentifier, GeneSetIdentifier>;
using ResolvedGenes = Seq<UniqueGeneIdentifier>;

public static class CatletSpecificationBuilder
{
    public static EitherAsync<Error, (CatletConfig ExpandedConfig, ResolvedGenes ResolvedGenes)> Build(
        CatletConfig catletConfig,
        Architecture architecture,
        IGenePoolReader genePoolReader,
        ILocalGenePoolReader localGenePoolReader,
        CancellationToken cancellation) =>
        from resolveResult in ResolveConfig(catletConfig, genePoolReader, cancellation)
        let resolvedGeneSets = resolveResult.ResolvedGeneSets
        let parentConfigs = resolveResult.ResolvedCatlets
        from resolvedConfig in CatletGeneResolving.ResolveGeneSetIdentifiers(catletConfig, resolvedGeneSets)
            .MapLeft(e => Error.New("Could not resolve genes in the catlet config.", e))
            .ToAsync()
        from breedingResult in CatletPedigree.Breed(catletConfig, resolvedGeneSets, parentConfigs)
            .MapLeft(e => Error.New("Could not breed the catlet.", e))
            .ToAsync()
        let bredConfigWithDefaults = CatletConfigDefaults.ApplyDefaults(breedingResult.Config)
        from genes in CatletGeneCollecting.CollectGenes(bredConfigWithDefaults)
            .ToEither().ToAsync()
            .MapLeft(errors => Error.New("The catlet config contains invalid genes.", Error.Many(errors)))
        from resolvedGenes in ResolveGenes(genes, architecture, genePoolReader, cancellation)
        from expandedConfig in CatletFeeding.Feed(
            bredConfigWithDefaults,
            resolvedGenes,
            localGenePoolReader)
            .MapLeft(e => Error.New("Could not feed the catlet.", e))
        select (expandedConfig, resolvedGenes);

    public static EitherAsync<Error, (GeneSetMap ResolvedGeneSets, CatletMap ResolvedCatlets)> ResolveConfig(
        CatletConfig catletConfig,
        IGenePoolReader genePoolReader,
        CancellationToken cancellationToken) =>
        from genesetsFromConfig in ResolveGeneSets(catletConfig, GeneSetMap.Empty, genePoolReader, cancellationToken)
            .MapLeft(e => Error.New("Could not resolve genes in the catlet config.", e))
        from result in ResolveParent(catletConfig.Parent, genesetsFromConfig, new CatletMap(),
            Seq<AncestorInfo>(), genePoolReader, cancellationToken)
        select result;

    private static EitherAsync<Error, (GeneSetMap ResolvedGeneSets, CatletMap ResolvedCatlets)> ResolveParent(
        Option<string> parentId,
        GeneSetMap resolvedGeneSets,
        CatletMap resolvedCatlets,
        Seq<AncestorInfo> visitedAncestors,
        IGenePoolReader geneProvider,
        CancellationToken cancellationToken) =>
        from validParentId in parentId.Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .MapLeft(e => Error.New("The parent ID is invalid.", e))
            .MapLeft(e => CreateError(visitedAncestors, e))
            .ToAsync()
        from result in validParentId.Match(
            Some: id => ResolveParent(id, resolvedGeneSets, resolvedCatlets,
                    visitedAncestors, geneProvider, cancellationToken),
            None: () => (resolvedGeneSets, resolvedCatlets))
        select result;

    private static EitherAsync<Error, (GeneSetMap ResolvedGeneSets, CatletMap ResolvedCatlets)> ResolveParent(
        GeneSetIdentifier id,
        GeneSetMap resolvedGeneSets,
        CatletMap resolvedCatlets,
        Seq<AncestorInfo> visitedAncestors,
        IGenePoolReader geneProvider,
        CancellationToken cancellationToken) =>
        from resolvedId in resolvedGeneSets.Find(id)
            .ToEitherAsync(Error.New($"Could not resolve parent ID '{id}'."))
            .MapLeft(e => CreateError(visitedAncestors, e))
        let updatedVisitedAncestors = visitedAncestors.Add(new AncestorInfo(id, resolvedId))
        from _ in CatletPedigree.ValidateAncestorChain(updatedVisitedAncestors)
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
            .ToAsync()
        from config in ReadCatletConfig(resolvedId, geneProvider, cancellationToken)
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from resolveResult in ResolveGeneSets(config, resolvedGeneSets, geneProvider, cancellationToken)
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from parentsResult in ResolveParent(config.Parent, resolveResult, resolvedCatlets,
            updatedVisitedAncestors, geneProvider, cancellationToken)
        select (
            parentsResult.ResolvedGeneSets,
            parentsResult.ResolvedCatlets.Add(resolvedId, config));

    private static EitherAsync<Error, GeneSetMap> ResolveGeneSets(
        CatletConfig catletConfig,
        GeneSetMap resolvedGeneSets,
        IGenePoolReader geneProvider,
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
        IGenePoolReader geneProvider,
        CancellationToken cancellationToken) =>
        resolvedGeneSets.Find(geneSetId).Match(
            Some: _ => resolvedGeneSets,
            None: () =>
                from resolvedGeneSet in ResolveGeneSet(geneSetId, geneProvider, cancellationToken)
                select resolvedGeneSets.Add(geneSetId, resolvedGeneSet));

    private static EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(
        GeneSetIdentifier geneSetId,
        IGenePoolReader geneProvider,
        CancellationToken cancellationToken) =>
        from resolved in ResolveGeneSet(geneSetId, Seq<GeneSetIdentifier>(), geneProvider, cancellationToken)
            .MapLeft(e => Error.New($"Could not resolve the gene set tag '{geneSetId}'.", e))
        select resolved;

    private static EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(
        GeneSetIdentifier geneSetId,
        Seq<GeneSetIdentifier> visited,
        IGenePoolReader genePoolReader,
        CancellationToken cancellationToken) =>
        from _ in guardnot(visited.Contains(geneSetId),
                Error.New($"Detected loop in gene set references: {string.Join(" -> ", visited.Add(geneSetId))}"))
            .ToEitherAsync()
        let maxChainLength = EryphConstants.Limits.MaxGeneSetReferenceDepth
        from __ in guardnot(visited.Count >= maxChainLength,
            Error.New($"The chain of gene set references is too long (up to {maxChainLength} references are allowed)."))
        from resolvedGeneSetId in genePoolReader.ResolveGeneSet(geneSetId, cancellationToken)
        from result in geneSetId == resolvedGeneSetId
            ? RightAsync<Error, GeneSetIdentifier>(resolvedGeneSetId)
            : ResolveGeneSet(resolvedGeneSetId, visited.Add(geneSetId), genePoolReader, cancellationToken)
        select result;

    public static EitherAsync<Error, CatletConfig> ReadCatletConfig(
        GeneSetIdentifier geneSetId,
        IGenePoolReader genepoolReader,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        let uniqueId = new UniqueGeneIdentifier(
            GeneType.Catlet,
            new GeneIdentifier(geneSetId, GeneName.New("catlet")),
            // Catlets are never architecture-specific. Hence, we hardcode any here.
            Architecture.New(EryphConstants.AnyArchitecture))
        from json in genepoolReader.GetGeneContent(uniqueId, cancellationToken)
        from config in Try(() => CatletConfigJsonSerializer.Deserialize(json))
            .ToEither(ex => Error.New($"Could not deserialize catlet config '{geneSetId}'.", Error.New(ex)))
            .ToAsync()
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


    public static EitherAsync<Error, Seq<UniqueGeneIdentifier>> ResolveGenes(
        Seq<GeneIdentifierWithType> genes,
        Architecture catletArchitecture,
        IGenePoolReader genePoolReader,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        let geneSetIds = genes.Map(iwt => iwt.GeneIdentifier.GeneSet).Distinct()
        from cachedGenes in geneSetIds
            .Map(geneSetId => genePoolReader.GetGenes(geneSetId, cancellationToken))
            .SequenceSerial()
            .Map(r => r.Map(kv => kv.Keys.ToSeq()).Flatten())
        //from result in genes.ToSeq().Fold<Validation<Error, ArchitectureMap>>(
        //    new ArchitectureMap(),
        //    (state, geneId) => state.Bind(m =>
        //        ResolveArchitecture(geneId, catletArchitecture, m, genePoolReader, cancellationToken)))
        from result in genes
            .Map(g => from architecture in ResolveArchitecture(g, catletArchitecture, cachedGenes)
                select new UniqueGeneIdentifier(g.GeneType, g.GeneIdentifier, architecture))
            .Sequence()
            .ToEither().ToAsync()
            .MapLeft(errors => Error.New("PENGH", Error.Many(errors)))
        select result;

    private static Validation<Error, ArchitectureMap> ResolveArchitecture(
        GeneIdentifierWithType geneIdWithType,
        Architecture catletArchitecture,
        ArchitectureMap resolvedArchitectures,
        Seq<UniqueGeneIdentifier> cachedGenes) =>
        resolvedArchitectures.Find(geneIdWithType).Match(
            Some: _ => resolvedArchitectures,
            None: () =>
                from resolvedArchitecture in ResolveArchitecture(geneIdWithType, catletArchitecture, cachedGenes)
                select resolvedArchitectures.Add(geneIdWithType, resolvedArchitecture));

    private static Validation<Error, Architecture> ResolveArchitecture(
        GeneIdentifierWithType geneIdWithType,
        Architecture catletArchitecture,
        Seq<UniqueGeneIdentifier> cachedGenes) =>
        from architecture in FindBestArchitecture(
            geneIdWithType.GeneIdentifier.GeneSet,
            geneIdWithType.GeneType,
            geneIdWithType.GeneIdentifier.GeneName,
            catletArchitecture,
            cachedGenes)
        //from manifest in genePoolReader.GetGeneSetTagManifest(geneIdWithType.GeneIdentifier.GeneSet, cancellationToken)
        //from manifest in optionalManifest.ToEitherAsync(
        //    Error.New($"The gene set '{geneIdWithType.GeneIdentifier.GeneSet}' is not available in the gene pool."))
        //from architecture in GeneSetTagManifestUtils.FindBestArchitecture(
        //    manifest.Manifest, catletArchitecture, geneIdWithType.GeneType, geneIdWithType.GeneIdentifier.GeneName).ToAsync()
        from validArchitecture in architecture.ToValidation(
            Error.New($"The gene '{geneIdWithType}' is not compatible with the hypervisor and/or processor architecture."))
        select validArchitecture;

    private static Validation<Error, Option<Architecture>> FindBestArchitecture(
        GeneSetIdentifier geneSetId,
        GeneType geneType,
        GeneName geneName,
        Architecture catletArchitecture,
        Seq<UniqueGeneIdentifier> uniqueGeneIds) =>
        from _ in Success<Error, Unit>(unit)
        let filteredByGeneSet = uniqueGeneIds.Filter(i => i.Id.GeneSet == geneSetId)
        let filteredByTypeAndName = filteredByGeneSet
            .Filter(i => i.GeneType == geneType && i.Id.GeneName == geneName)
        let architectures = filteredByTypeAndName
            .Map(i => i.Architecture)
        let bestArchitecture = architectures.Find(ga => ga == catletArchitecture)
                               | architectures.Find(ga =>
                                   ga.Hypervisor == catletArchitecture.Hypervisor
                                   && ga.ProcessorArchitecture == ProcessorArchitecture.New("any"))
                               | architectures.Find(ga => ga.IsAny)

        select bestArchitecture;
}
