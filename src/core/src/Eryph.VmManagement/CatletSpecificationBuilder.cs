using System.Threading;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

using CatletMap = HashMap<GeneSetIdentifier, CatletConfig>;
using GeneSetMap = HashMap<GeneSetIdentifier, GeneSetIdentifier>;
using ResolvedGenes = HashMap<UniqueGeneIdentifier, GeneHash>;

public static class CatletSpecificationBuilder
{
    public static EitherAsync<Error, (CatletConfig ExpandedConfig, ResolvedGenes ResolvedGenes)> Build(
        CatletConfig catletConfig,
        Architecture architecture,
        IGenePoolReader genePoolReader,
        CancellationToken cancellation) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let configWithEarlyDefaults = CatletConfigDefaults.ApplyDefaultNetwork(catletConfig)
        from resolveResult in ResolveConfig(configWithEarlyDefaults, genePoolReader, cancellation)
        let resolvedGeneSets = resolveResult.ResolvedGeneSets
        let parentConfigs = resolveResult.ResolvedCatlets
        from breedingResult in CatletPedigree.Breed(configWithEarlyDefaults, resolvedGeneSets, parentConfigs)
            .MapLeft(e => Error.New("Could not breed the catlet.", e))
            .ToAsync()
        // Normalize after the breeding as the normalization adds default values.
        // These default values could confuse the breeding as it cannot differentiate
        // between user-provided values and default values.
        from normalizedConfig in CatletConfigNormalizer.Normalize(breedingResult.Config)
            .ToEither()
            .MapLeft(errors => Error.New("Could not normalize the catlet config.", Error.Many(errors)))
            .ToAsync()
        let normalizedWithDefaults = CatletConfigDefaults.ApplyDefaults(normalizedConfig)
        from genes in CatletGeneCollecting.CollectGenes(normalizedWithDefaults)
            .ToEither().ToAsync()
            .MapLeft(errors => Error.New("The catlet config contains invalid genes.", Error.Many(errors)))
        from resolvedGenes in ResolveGenes(genes, architecture, genePoolReader, cancellation)
        from expandedConfig in CatletFeeding.Feed(
            normalizedWithDefaults,
            resolvedGenes,
            genePoolReader)
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
        from genes in geneProvider.GetGenes(resolvedId, cancellationToken)
        let catletGeneId = new UniqueGeneIdentifier(
            GeneType.Catlet,
            new GeneIdentifier(resolvedId, GeneName.New("catlet")),
            // Catlets are never architecture-specific. Hence, we hard code any here.
            Architecture.New(EryphConstants.AnyArchitecture))
        from catletGeneHash in genes.Find(catletGeneId)
            .ToEitherAsync(Error.New($"The gene set {id} is the parent of a catlet but does not contain a catlet gene."))
        from config in ReadCatletConfig(catletGeneId, catletGeneHash, geneProvider, cancellationToken)
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
        from resolvedGeneSetId in genePoolReader.GetReferencedGeneSet(geneSetId, cancellationToken)
        from result in resolvedGeneSetId.Match(
            Some: gsi => ResolveGeneSet(gsi, visited.Add(geneSetId), genePoolReader, cancellationToken),
            None: () => RightAsync<Error, GeneSetIdentifier>(geneSetId))
        select result;

    public static EitherAsync<Error, CatletConfig> ReadCatletConfig(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        IGenePoolReader genePoolReader,
        CancellationToken cancellationToken) =>
        from json in genePoolReader.GetGeneContent(uniqueGeneId, geneHash, cancellationToken)
        from genes in genePoolReader.GetGenes(uniqueGeneId.Id.GeneSet, cancellationToken)
        from config in Try(() => CatletConfigJsonSerializer.Deserialize(json))
            .ToEither(ex => Error.New($"Could not deserialize catlet config in gene {uniqueGeneId} ({geneHash}).", Error.New(ex)))
            .ToAsync()
        from normalizedConfig in NormalizeSources(config, uniqueGeneId.Id.GeneSet, genes.Keys.ToSeq())
            .MapLeft(e => Error.New($"Cannot normalize the gene pool sources in the catlet gene {uniqueGeneId} ({geneHash}.", e))
        select normalizedConfig;

    private static EitherAsync<Error, CatletConfig> NormalizeSources(
        CatletConfig config,
        GeneSetIdentifier geneSetId,
        Seq<UniqueGeneIdentifier> genes) =>
        from normalizedDrives in config.Drives.ToSeq()
            .Map(d => NormalizeSource(d, genes))
            .SequenceSerial()
        from normalizedFodder in config.Fodder.ToSeq()
            .Map(f => NormalizeSource(f, geneSetId))
            .SequenceSerial()
        select config.CloneWith(c =>
        {
            c.Drives = normalizedDrives.ToArray();
            c.Fodder = normalizedFodder.ToArray();
        });

    private static EitherAsync<Error, FodderConfig> NormalizeSource(
        FodderConfig config,
        GeneSetIdentifier geneSetId) =>
        from _ in RightAsync<Error, Unit>(unit)
        select config.CloneWith(c =>
        {
            c.Source = Optional(c.Source).Filter(notEmpty)
                .IfNone(() => new GeneIdentifier(geneSetId, GeneName.New("catlet")).Value);
        });

    private static EitherAsync<Error, CatletDriveConfig> NormalizeSource(
        CatletDriveConfig config,
        Seq<UniqueGeneIdentifier> genes) =>
        from _ in RightAsync<Error, Unit>(unit)
        let driveType = config.Type ?? CatletDriveType.Vhd
        let source = GeneName.NewOption(config.Name)
            .Bind(n => genes.Find(g => g.GeneType is GeneType.Volume && g.Id.GeneName == n))
            .Map(g => g.Id.Value)
        select config.CloneWith(c =>
        {
            c.Source = driveType != CatletDriveType.Vhd || notEmpty(c.Source)
                ? c.Source
                : source.IfNoneUnsafe((string?)null);
        });

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


    public static EitherAsync<Error, HashMap<UniqueGeneIdentifier, GeneHash>> ResolveGenes(
        Seq<GeneIdentifierWithType> genes,
        Architecture catletArchitecture,
        IGenePoolReader genePoolReader,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        let geneSetIds = genes.Map(iwt => iwt.GeneIdentifier.GeneSet).Distinct()
        from cachedGenes in geneSetIds
            .Map(geneSetId => genePoolReader.GetGenes(geneSetId, cancellationToken))
            .SequenceSerial()
            .Map(r => r.Map(m => m.ToSeq()).Flatten().ToHashMap())
        from result in genes
            .Distinct()
            .Map(g => ResolveGene(g, catletArchitecture, cachedGenes))
            .Sequence()
            .ToEither().ToAsync()
            .Map(r => r.ToHashMap())
            .MapLeft(errors => Error.New("Could not resolve some genes.", Error.Many(errors)))
        select result;

    public static Validation<Error, (UniqueGeneIdentifier, GeneHash)> ResolveGene(
        GeneIdentifierWithType geneIdWithType,
        Architecture catletArchitecture,
        HashMap<UniqueGeneIdentifier, GeneHash> cachedGenes) =>
        from resolvedId in ResolveGene(geneIdWithType, catletArchitecture, cachedGenes.Keys.ToSeq())
        from geneHash in cachedGenes.Find(resolvedId).ToValidation(
            Error.New($"BUG! Cannot find resolved gene {resolvedId}."))
        select (resolvedId, geneHash);

    private static Validation<Error, UniqueGeneIdentifier> ResolveGene(
        GeneIdentifierWithType geneIdWithType,
        Architecture catletArchitecture,
        Seq<UniqueGeneIdentifier> cachedGenes) =>
        from _1 in Success<Error, Unit>(unit)
        let filteredGenes = cachedGenes
            .Filter(i => i.GeneType == geneIdWithType.GeneType && i.Id == geneIdWithType.GeneIdentifier)
        from _2 in guard(filteredGenes.Count > 0, Error.New($"The gene {geneIdWithType} does not exist."))
        let hypervisorCompatibleGenes = filteredGenes
            .Filter(g => g.Architecture.Hypervisor == catletArchitecture.Hypervisor
                         || g.Architecture.Hypervisor.IsAny)
        from _3 in guard(
            hypervisorCompatibleGenes.Count > 0,
            Error.New($"The gene {geneIdWithType} is not compatible with the hypervisor {catletArchitecture.Hypervisor}."))
        let processorCompatibleGenes = hypervisorCompatibleGenes
            .Filter(g => g.Architecture.ProcessorArchitecture == catletArchitecture.ProcessorArchitecture
                         || g.Architecture.ProcessorArchitecture.IsAny)
        from _4 in guard(
            processorCompatibleGenes.Count > 0,
            Error.New($"The gene {geneIdWithType} is not compatible with the processor architecture {catletArchitecture.ProcessorArchitecture}."))
        let bestMatch = processorCompatibleGenes.Find(g => g.Architecture == catletArchitecture)
                        | processorCompatibleGenes.Find(g => g.Architecture.Hypervisor == catletArchitecture.Hypervisor
                                                             && g.Architecture.ProcessorArchitecture.IsAny)
                        | processorCompatibleGenes.Find(g => g.Architecture.IsAny)
        from result in bestMatch.ToValidation(
            Error.New($"BUG! Could not find best match for gene '{geneIdWithType}'."))
        select result;
}
