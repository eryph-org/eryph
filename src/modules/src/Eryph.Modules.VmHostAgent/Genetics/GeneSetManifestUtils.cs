using System;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using LanguageExt;

using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal static class GeneSetManifestUtils
{
    public static Option<string> FindGeneHash(
        GenesetTagManifestData manifest,
        GeneType geneType,
        GeneName geneName) =>
        (geneType switch
        {
            GeneType.Catlet => Optional(manifest.CatletGene),
            GeneType.Volume => manifest.VolumeGenes.ToSeq()
                .Find(x => x.Name == geneName.Value)
                .Bind(x => Optional(x.Hash)),
            GeneType.Fodder => manifest.FodderGenes.ToSeq()
                .Find(x => x.Name == geneName.Value)
                .Bind(x => Optional(x.Hash)),
            _ => throw new ArgumentOutOfRangeException(nameof(geneType))
        }).Filter(notEmpty);
}
