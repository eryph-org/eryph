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
            .MapLeft(e => Error.New($"The fodder genes in the manifest of the gene set '{manifest.Geneset}' are invalid.", e))
        from volumeGenes in GetGenes(geneSetId, GeneType.Volume, manifest.VolumeGenes.ToSeq())
            .MapLeft(e => Error.New($"The volume genes in the manifest of the gene set '{manifest.Geneset}' are invalid.", e))
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
        select genes.ToHashMap();

    private static Either<Error, (UniqueGeneIdentifier, GeneHash)> GetGene(
        GeneSetIdentifier geneSetId,
        GeneType geneType,
        GeneReferenceData geneReference) =>
        from geneName in GeneName.NewEither(geneReference.Name)
            .MapLeft(e =>
                Error.New(
                    $"The name '{geneReference.Name}' of a {geneType} gene of the gene set {geneSetId} is invalid.",
                    e))
        from architecture in Optional(geneReference.Architecture).Match(
                Some: Architecture.NewEither,
                None: () => Architecture.New(EryphConstants.AnyArchitecture))
            .MapLeft(e => Error.New($"The architecture of the {geneType} gene {geneName} of the gene set {geneSetId} is invalid.", e))
        let uniqueGeneId = new UniqueGeneIdentifier(geneType, new GeneIdentifier(geneSetId, geneName), architecture)
        from geneHash in GeneHash.NewEither(geneReference.Hash)
            .MapLeft(e => Error.New($"The hash of the gene {uniqueGeneId} is invalid.", e))
        select (uniqueGeneId, geneHash);
}
