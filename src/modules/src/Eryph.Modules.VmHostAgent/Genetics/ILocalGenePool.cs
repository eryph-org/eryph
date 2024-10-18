using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal interface ILocalGenePool: IGenePool
{
    EitherAsync<Error, Unit> MergeGeneParts(
        GeneInfo geneInfo,
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel);

    EitherAsync<Error, GeneSetInfo> CacheGeneSet(
        GeneSetInfo geneSetInfo,
        CancellationToken cancel);

    EitherAsync<Error, GeneInfo> CacheGene(
        GeneInfo geneInfo,
        GeneSetInfo geneSetInfo,
        CancellationToken cancel);

    EitherAsync<Error, GeneSetInfo> GetCachedGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken);

    EitherAsync<Error, Option<long>> GetCachedGeneSize(
        UniqueGeneIdentifier uniqueGeneId);

    EitherAsync<Error, string> GetGenePartPath(
        UniqueGeneIdentifier uniqueGeneId,
        string geneHash,
        string genePartHash);

    EitherAsync<Error, Unit> RemoveCachedGene(
        UniqueGeneIdentifier uniqueGeneId);
}
