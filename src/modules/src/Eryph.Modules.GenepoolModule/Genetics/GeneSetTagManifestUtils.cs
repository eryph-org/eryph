using System;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.Genepool.Genetics;

internal static class GeneSetTagManifestUtils
{
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
