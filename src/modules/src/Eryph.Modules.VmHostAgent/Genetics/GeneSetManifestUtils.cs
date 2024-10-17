using System;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using static LanguageExt.Seq;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal static class GeneSetManifestUtils
{
    public static Option<string> FindGeneHash(
        GenesetTagManifestData manifest,
        GeneType geneType,
        GeneArchitecture architecture,
        GeneName geneName) =>
        from _ in Some(unit)
        let hash = geneType switch
        {
            GeneType.Catlet => geneName == GeneName.New("catlet") && architecture.IsAny
                ? Optional(manifest.CatletGene)
                : None,
            GeneType.Volume => manifest.VolumeGenes.ToSeq()
                .Find(x => x.Name == geneName.Value && GeneArchitecture.NewOption(x.Architecture) == architecture)
                .Bind(x => Optional(x.Hash)),
            GeneType.Fodder => manifest.FodderGenes.ToSeq()
                .Find(x => x.Name == geneName.Value && GeneArchitecture.NewOption(x.Architecture) == architecture)
                .Bind(x => Optional(x.Hash)),
            _ => None
        }
        from validHash in hash.Filter(notEmpty)
        select validHash;

    public static Either<Error, Option<GeneArchitecture>> FindBestArchitecture(
        GenesetTagManifestData manifest,
        GeneType geneType,
        GeneArchitecture architecture,
        GeneName geneName) =>
        from _ in Right<Error, Unit>(unit)
        let architectures = geneType switch
        {
            GeneType.Catlet => Seq([GeneArchitecture.NewEither("any")]),
            GeneType.Volume => manifest.VolumeGenes.ToSeq()
                .Filter(x => x.Name == geneName.Value)
                .Map(x => GeneArchitecture.NewEither(x.Architecture)),
            GeneType.Fodder => manifest.FodderGenes.ToSeq()
                .Filter(x => x.Name == geneName.Value)
                .Map(x => GeneArchitecture.NewEither(x.Architecture)),
            _ => [Error.New($"The gene type {geneType} is not sup")]
        }
        from validArchitectures in architectures.Sequence()
            .MapLeft(e => Error.New($"The manifest of the gene set '{manifest.Geneset}' is invalid.", e))
        select FindBestArchitecture(architecture, validArchitectures);

    public static Either<Error, Option<GeneArchitecture>> FindBestArchitecture(
        GenesetTagManifestData manifest,
        GeneArchitecture catletArchitecture,
        GeneName geneName) =>
        from _ in Right<Error, Unit>(unit)
        let architectures = append(
            Seq([GeneArchitecture.NewEither("any")]).Filter(_ => geneName == GeneName.New("catlet")),
            manifest.VolumeGenes.ToSeq()
                .Filter(x => GeneName.NewOption(x.Name) == geneName)
                .Map(x => GeneArchitecture.NewEither(x.Architecture)),
            manifest.FodderGenes.ToSeq()
                .Filter(x => GeneName.NewOption(x.Name) == geneName)
                .Map(x => GeneArchitecture.NewEither(x.Architecture))
            )
        from validArchitectures in architectures.Sequence()
            .MapLeft(e => Error.New($"The manifest of the gene set '{manifest.Geneset}' is invalid.", e))
        select FindBestArchitecture(catletArchitecture, validArchitectures);

    private static Option<GeneArchitecture> FindBestArchitecture(
        GeneArchitecture architecture,
        Seq<GeneArchitecture> geneArchitectures) =>
        geneArchitectures.Find(ga => ga == architecture)
        | geneArchitectures.Find(ga =>
            ga.Hypervisor == architecture.Hypervisor && ga.ProcessorArchitecture == ProcessorArchitecture.New("any"))
        | geneArchitectures.Find(ga => ga.IsAny);
}
