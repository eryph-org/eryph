using System;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.Sys;
using Eryph.GenePool.Model;
using Eryph.Messages.Genes.Commands;
using LanguageExt;

namespace Eryph.Modules.GenePool.Genetics;

public interface IGeneProvider
{
    Aff<CancelRt, string> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash);
    
    Aff<CancelRt, GenesetTagManifestData> GetGeneSetManifest(
        GeneSetIdentifier geneSetId);

    Aff<CancelRt, PrepareGeneResponse> ProvideGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<string, int, Task> reportProgress);
}
