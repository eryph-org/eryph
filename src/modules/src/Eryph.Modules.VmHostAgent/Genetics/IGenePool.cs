using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal interface IGenePool
{
    EitherAsync<Error, GeneSetInfo> ProvideGeneSet(string path, GeneSetIdentifier genesetIdentifier, CancellationToken cancel);

    EitherAsync<Error, GeneInfo> RetrieveGene(GeneSetInfo imageInfo, GeneIdentifier geneIdentifier, string geneHash, CancellationToken cancel);

    EitherAsync<Error, long> RetrieveGenePart(GeneInfo geneInfo, string genePartHash, long availableSize, long totalSize, Func<string, int, Task<Unit>> reportProgress, Stopwatch stopwatch, CancellationToken cancel);

    public string PoolName { get; set; }
}