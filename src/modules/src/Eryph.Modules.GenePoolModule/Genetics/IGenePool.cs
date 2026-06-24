using System;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.Sys;
using LanguageExt;

namespace Eryph.Modules.GenePool.Genetics;

internal interface IGenePool
{
    public string PoolName { get; }

    Aff<CancelRt, Option<GeneSetInfo>> GetGeneSet(
        GeneSetIdentifier geneSetId);

    Aff<CancelRt, Option<GeneContentInfo>> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash);

    Aff<CancelRt, Option<Unit>> DownloadGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartsState partsState,
        string downloadPath,
        Func<long, long, Task> reportProgress);
}
