using System;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal static class GeneSetManifestUtils
{
    public static Option<string> FindGeneHash(
        GenesetTagManifestData manifest,
        GeneType geneType,
        Architecture architecture,
        GeneName geneName) =>
        from _ in Some(unit)
        let hash = geneType switch
        {
            GeneType.Catlet => geneName == GeneName.New("catlet") && architecture.IsAny
                ? Optional(manifest.CatletGene)
                : None,
            GeneType.Volume => manifest.VolumeGenes.ToSeq()
                .Find(x => x.Name == geneName.Value && Architecture.NewOption(x.Architecture ?? "any") == architecture)
                .Bind(x => Optional(x.Hash)),
            GeneType.Fodder => manifest.FodderGenes.ToSeq()
                .Find(x => x.Name == geneName.Value && Architecture.NewOption(x.Architecture ?? "any") == architecture)
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
        from _ in Right<Error, Unit>(unit)
        let architectures = geneType switch
        {
            GeneType.Catlet => Seq([Architecture.NewEither("any")]),
            GeneType.Volume => manifest.VolumeGenes.ToSeq()
                .Filter(x => x.Name == geneName.Value)
                .Map(x => Architecture.NewEither(x.Architecture ?? "any")),
            GeneType.Fodder => manifest.FodderGenes.ToSeq()
                .Filter(x => x.Name == geneName.Value)
                .Map(x => Architecture.NewEither(x.Architecture ?? "any")),
            _ => [Error.New($"The gene type {geneType} is not sup")]
        }
        from validArchitectures in architectures.Sequence()
            .MapLeft(e => Error.New($"The manifest of the gene set '{manifest.Geneset}' is invalid.", e))
        let bestArchitecture = validArchitectures.Find(ga => ga == architecture)
                               | validArchitectures.Find(ga =>
                                   ga.Hypervisor == architecture.Hypervisor
                                   && ga.ProcessorArchitecture == ProcessorArchitecture.New("any"))
                               | validArchitectures.Find(ga => ga.IsAny)
        select bestArchitecture;
}
