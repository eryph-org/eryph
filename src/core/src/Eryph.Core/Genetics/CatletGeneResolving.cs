using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

using GeneSetMap = HashMap<GeneSetIdentifier, GeneSetIdentifier>;
using ResolvedGenes = HashMap<UniqueGeneIdentifier, GeneHash>;

public static class CatletGeneResolving
{
    public static Either<Error, CatletConfig> ResolveGeneSetIdentifiers(
        CatletConfig catletConfig,
        GeneSetMap resolvedGeneSets) =>
        from resolvedParent in Optional(catletConfig.Parent)
            .Filter(notEmpty)
            .Map(p => GeneSetIdentifier.NewEither(p)
                .MapLeft(e => Error.New($"The parent source '{catletConfig.Parent}' is invalid.", e)))
            .BindT(geneSetId => ResolveGeneSetIdentifier(geneSetId, resolvedGeneSets))
            .Sequence()
        from resolvedDrives in catletConfig.Drives.ToSeq()
            .Map(driveConfig => ResolveGeneSetIdentifiers(driveConfig, resolvedGeneSets))
            .Sequence()
        from resolvedFodder in catletConfig.Fodder.ToSeq()
            .Map(fodderConfig => ResolveGeneSetIdentifiers(fodderConfig, resolvedGeneSets))
            .Sequence()
        select catletConfig.CloneWith(c =>
        {
            c.Parent = resolvedParent.Map(id => id.Value).IfNoneUnsafe((string)null);
            c.Drives = resolvedDrives.ToArray();
            c.Fodder = resolvedFodder.ToArray();
        });

    private static Either<Error, FodderConfig> ResolveGeneSetIdentifiers(
        FodderConfig fodderConfig,
        GeneSetMap resolvedGeneSets) =>
        from resolvedGeneIdentifier in Optional(fodderConfig.Source)
            .Filter(notEmpty)
            .Map(s => ResolveGeneSetIdentifier(s, resolvedGeneSets))
            .Sequence()
        select fodderConfig.CloneWith(c =>
        {
            c.Source = resolvedGeneIdentifier.Map(id => id.Value)
                .IfNoneUnsafe((string)null);
        });

    private static Either<Error, CatletDriveConfig> ResolveGeneSetIdentifiers(
        CatletDriveConfig driveConfig,
        GeneSetMap resolvedGeneSets) =>
        from resolvedSource in Optional(driveConfig.Source)
            .Filter(s => s.StartsWith("gene:"))
            .Map(s => ResolveGeneSetIdentifier(s, resolvedGeneSets))
            .MapT(geneId => geneId.Value)
            .Sequence()
        select driveConfig.CloneWith(c =>
        {
            c.Source = resolvedSource.IfNoneUnsafe(driveConfig.Source);
        });

    private static Either<Error, GeneIdentifier> ResolveGeneSetIdentifier(
        string geneIdentifier,
        GeneSetMap resolvedGeneSets) =>
        from validGeneId in GeneIdentifier.NewEither(geneIdentifier)
            .MapLeft(e => Error.New($"The gene ID '{geneIdentifier}' is invalid.", e))
        from resolvedGeneSetId in ResolveGeneSetIdentifier(validGeneId.GeneSet, resolvedGeneSets)
        select new GeneIdentifier(resolvedGeneSetId, validGeneId.GeneName);

    private static Either<Error, GeneSetIdentifier> ResolveGeneSetIdentifier(
        GeneSetIdentifier geneSetId,
        GeneSetMap resolvedGeneSets) =>
        resolvedGeneSets.Find(geneSetId)
            .ToEither(Error.New($"The gene set '{geneSetId}' could not be resolved."));

    public static Either<Error, ResolvedGenes> ResolveGenes(
        Seq<GeneIdentifierWithType> genes,
        Architecture catletArchitecture,
        ResolvedGenes cachedGenes) =>
        from result in genes
            .Distinct()
            .Map(g => ResolveGene(g, catletArchitecture, cachedGenes))
            .Sequence()
            .ToEither()
            .Map(r => r.ToHashMap())
            .MapLeft(errors => Error.New("Could not resolve some genes.", Error.Many(errors)))
        select result;

    private static Validation<Error, (UniqueGeneIdentifier, GeneHash)> ResolveGene(
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
