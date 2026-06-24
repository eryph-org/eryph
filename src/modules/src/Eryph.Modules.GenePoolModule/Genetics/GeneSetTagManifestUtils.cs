using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.GenePool.Genetics;

public static class GeneSetTagManifestUtils
{
    public static Either<Error, HashMap<UniqueGeneIdentifier, GeneHash>> GetGenes(
        GenesetTagManifestData manifest) =>
        from geneSetId in GeneSetIdentifier.NewEither(manifest.Geneset)
            .MapLeft(e => Error.New($"The gene set ID '{manifest.Geneset}' in the manifest is invalid.", e))
        from catletGenes in GetCatletGene(geneSetId, manifest.CatletGene)
        from fodderGenes in GetGenes(geneSetId, GeneType.Fodder, manifest.FodderGenes.ToSeq())
            .MapLeft(e =>
                Error.New($"The fodder genes in the manifest of the gene set '{manifest.Geneset}' are invalid.", e))
        from volumeGenes in GetGenes(geneSetId, GeneType.Volume, manifest.VolumeGenes.ToSeq())
            .MapLeft(e =>
                Error.New($"The volume genes in the manifest of the gene set '{manifest.Geneset}' are invalid.", e))
        select catletGenes + fodderGenes + volumeGenes;

    private static Either<Error, HashMap<UniqueGeneIdentifier, GeneHash>> GetCatletGene(
        GeneSetIdentifier geneSetId,
        Option<string> geneHash) =>
        from parsedGeneHash in geneHash
            .Filter(notEmpty)
            .Map(GeneHash.NewEither)
            .Sequence()
            .MapLeft(e => Error.New("The hash of the catlet gene is invalid.", e))
        let geneId = new GeneIdentifier(geneSetId, GeneName.New("catlet"))
        let uniqueGeneId = new UniqueGeneIdentifier(GeneType.Catlet, geneId, Architecture.New("any"))
        select parsedGeneHash.Map(h => (uniqueGeneId, h)).ToHashMap();

    private static Either<Error, HashMap<UniqueGeneIdentifier, GeneHash>> GetGenes(
        GeneSetIdentifier geneSetId,
        GeneType geneType,
        Seq<GeneReferenceData> geneReferences) =>
        from genes in geneReferences
            .Map(geneReference => GetGene(geneSetId, geneType, geneReference))
            .Sequence()
        select genes.Somes().ToHashMap();

    private static Either<Error, Option<(UniqueGeneIdentifier, GeneHash)>> GetGene(
        GeneSetIdentifier geneSetId,
        GeneType geneType,
        GeneReferenceData geneReference)
    {
        // Genes that target a hypervisor this version of eryph does not
        // understand are ignored, so the gene pool can be extended with new
        // hypervisors without breaking older clients. Malformed manifest
        // entries (and known hypervisors with an unsupported processor) are
        // still treated as errors.
        if (HasUnknownHypervisor(geneReference.Architecture))
            return Right<Error, Option<(UniqueGeneIdentifier, GeneHash)>>(None);

        return
            from architecture in Optional(geneReference.Architecture).Match(
                    Architecture.NewEither,
                    () => Architecture.New(EryphConstants.AnyArchitecture))
                .MapLeft(e => Error.New(
                    $"The architecture '{geneReference.Architecture}' of a {geneType} gene of the gene set {geneSetId} is invalid.",
                    e))
            from geneName in GeneName.NewEither(geneReference.Name)
                .MapLeft(e =>
                    Error.New(
                        $"The name '{geneReference.Name}' of a {geneType} gene of the gene set {geneSetId} is invalid.",
                        e))
            let uniqueGeneId = new UniqueGeneIdentifier(geneType, new GeneIdentifier(geneSetId, geneName), architecture)
            from geneHash in GeneHash.NewEither(geneReference.Hash)
                .MapLeft(e => Error.New($"The hash of the gene {uniqueGeneId} is invalid.", e))
            select Some((uniqueGeneId, geneHash));
    }

    // Returns true only when the architecture string is structurally well-formed
    // (two non-empty slash-separated parts) and the hypervisor component is not
    // one of the values understood by this version of eryph. Malformed strings
    // are intentionally NOT classified as "unknown" so they continue to surface
    // as manifest errors via Architecture.NewEither.
    private static bool HasUnknownHypervisor(string? rawArchitecture)
    {
        if (rawArchitecture is null)
            return false;
        var parts = rawArchitecture.Split('/');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;
        return Hypervisor.NewEither(parts[0]).IsLeft;
    }
}
