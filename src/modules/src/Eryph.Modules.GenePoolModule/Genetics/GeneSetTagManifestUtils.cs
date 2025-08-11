using System;
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

    public static Option<string> FindGeneHash(
        GenesetTagManifestData manifest,
        GeneType geneType,
        GeneName geneName,
        Architecture architecture) =>
        from _ in Some(unit)
        let hash = geneType switch
        {
            GeneType.Catlet => geneName == GeneName.New("catlet") && architecture.IsAny
                ? Optional(manifest.CatletGene)
                : None,
            GeneType.Volume => manifest.VolumeGenes.ToSeq()
                .Find(x => GeneName.NewOption(x.Name) == geneName
                           && Architecture.NewOption(x.Architecture ?? "any") == architecture)
                .Bind(x => Optional(x.Hash)),
            GeneType.Fodder => manifest.FodderGenes.ToSeq()
                .Find(x => GeneName.NewOption(x.Name) == geneName
                           && Architecture.NewOption(x.Architecture ?? "any") == architecture)
                .Bind(x => Optional(x.Hash)),
            _ => None
        }
        from validHash in hash.Filter(notEmpty)
        select validHash;

    public static Either<Error, Option<Architecture>> FindBestArchitecture(
        GenesetTagManifestData manifest,
        Architecture architecture,
        GeneType geneType,
        GeneName geneName) =>
        from _ in guard(geneType is GeneType.Catlet or GeneType.Fodder or GeneType.Volume,
                Error.New($"The gene type '{geneType}' is not supported."))
            .ToEither()
        let genes = geneType switch
        {
            GeneType.Catlet => Optional(manifest.CatletGene)
                .Filter(notEmpty)
                .Map(_ => (Name: "catlet", Architecture: "any"))
                .ToSeq(),
            GeneType.Volume => manifest.VolumeGenes.ToSeq()
                .Map(x => (x.Name, x.Architecture)),
            GeneType.Fodder => manifest.FodderGenes.ToSeq()
                .Map(x => (x.Name, x.Architecture)),
            _ => Empty
        }
        from validGenes in genes
            .Map(g => from n in GeneName.NewEither(g.Name)
                      from a in Architecture.NewEither(g.Architecture ?? EryphConstants.AnyArchitecture)
                      select (Name: n, Architecture: a))
            .Sequence()
            .MapLeft(e => Error.New($"The manifest of the gene set '{manifest.Geneset}' is invalid.", e))
        let filteredArchitectures = validGenes
            .Filter(g => g.Name == geneName)
            .Map(g => g.Architecture)
        let bestArchitecture = filteredArchitectures.Find(ga => ga == architecture)
                               | filteredArchitectures.Find(ga =>
                                   ga.Hypervisor == architecture.Hypervisor
                                   && ga.ProcessorArchitecture == ProcessorArchitecture.New("any"))
                               | filteredArchitectures.Find(ga => ga.IsAny)
        select bestArchitecture;
}
