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
    EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancel);

    /// <summary>
    /// Fetches the gene set 
    /// </summary>
    /// <param name="geneSetId"></param>
    /// <returns></returns>
    Aff<CancelRt, Option<GeneSetInfo>> GetGeneSet(
        GeneSetIdentifier geneSetId);

    EitherAsync<Error, GeneInfo> RetrieveGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash, 
        CancellationToken cancel);

    EitherAsync<Error, long> RetrieveGenePart(
        GeneInfo geneInfo,
        string genePartHash,
        string genePartPath,
        long availableSize,
        long totalSize,
        Func<string, int, Task<Unit>> reportProgress,
        Stopwatch stopwatch,
        CancellationToken cancel);

    EitherAsync<Error, GeneContentInfo> RetrieveGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken);

    Aff<CancelRt, Option<GeneContentInfo>> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash);

    Aff<CancelRt, Option<GenePartsInfo>> DownloadGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartsInfo geneParts);
    
    Aff<CancelRt, Option<Unit>> DownloadGene2(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartsState partsState,
        string downloadPath,
        Func<long, long, Task<Unit>> reportProgress);

    public string PoolName { get; }
}
