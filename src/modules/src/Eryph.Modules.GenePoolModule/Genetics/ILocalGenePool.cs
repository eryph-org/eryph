using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.GenePool.Genetics;

internal interface ILocalGenePool : IGenePool
{
    EitherAsync<Error, Unit> MergeGeneParts(
        GeneInfo geneInfo,
        Func<long, long, Task<Unit>> reportProgress,
        CancellationToken cancellationToken);

    EitherAsync<Error, GeneSetInfo> CacheGeneSet(
        GeneSetInfo geneSetInfo,
        CancellationToken cancellationToken);

    EitherAsync<Error, GeneInfo> CacheGene(
        GeneInfo geneInfo,
        CancellationToken cancellationToken);

    EitherAsync<Error, Option<GeneSetInfo>> GetCachedGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken);

    EitherAsync<Error, Option<string>> GetCachedGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken);

    EitherAsync<Error, Option<long>> GetCachedGeneSize(
        UniqueGeneIdentifier uniqueGeneId);

    EitherAsync<Error, string> GetGenePartPath(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartHash genePartHash);

    EitherAsync<Error, Unit> RemoveCachedGene(
        UniqueGeneIdentifier uniqueGeneId);

    EitherAsync<Error, string> CacheGeneContent(
        GeneContentInfo geneContentInfo,
        CancellationToken cancellationToken);
    
    EitherAsync<Error, Option<GenePartsInfo>> GetDownloadedGeneParts(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<long, long, Task<Unit>> reportProgress,
        CancellationToken cancellationToken);

    EitherAsync<Error, bool> HasGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken);
}
