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
        from _ in Some(unit)
        let hash = geneType switch
        {
            GeneType.Catlet => geneName == GeneName.New("catlet")
                ? Optional(manifest.CatletGene)
                : None,
            GeneType.Volume => manifest.VolumeGenes.ToSeq()
                .Find(x => x.Name == geneName.Value)
                .Bind(x => Optional(x.Hash)),
            GeneType.Fodder => manifest.FodderGenes.ToSeq()
                .Find(x => x.Name == geneName.Value)
                .Bind(x => Optional(x.Hash)),
            _ => None
        }
        from validHash in hash.Filter(notEmpty)
        select validHash;
}
