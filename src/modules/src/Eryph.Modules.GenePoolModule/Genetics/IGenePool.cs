using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.Sys;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.GenePool.Genetics;

internal interface IGenePool
{
    /// <summary>
    /// Fetches the gene set 
    /// </summary>
    /// <param name="geneSetId"></param>
    /// <returns></returns>
    Aff<CancelRt, Option<GeneSetInfo>> GetGeneSet(
        GeneSetIdentifier geneSetId);


    Aff<CancelRt, Option<GeneContentInfo>> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash);
    
    Aff<CancelRt, Option<Unit>> DownloadGene2(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartsState partsState,
        string downloadPath,
        Func<long, long, Task<Unit>> reportProgress);

    public string PoolName { get; }
}
