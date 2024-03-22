using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal interface ILocalGenePool: IGenePool
{
    EitherAsync<Error, Unit> MergeGenes(GeneInfo geneInfo, GeneSetInfo imageInfo,
        Func<string, int, Task<Unit>> reportProgress, CancellationToken cancel);
    EitherAsync<Error, GeneSetInfo> ProvideFallbackGeneSet(string path, GeneSetIdentifier genesetIdentifier, CancellationToken cancel);

    EitherAsync<Error, GeneSetInfo> CacheGeneSet(string path, GeneSetInfo genesetInfo, CancellationToken cancel);
    EitherAsync<Error, GeneInfo> CacheGene(GeneInfo geneInfo, GeneSetInfo genesetInfo, CancellationToken cancel);

}