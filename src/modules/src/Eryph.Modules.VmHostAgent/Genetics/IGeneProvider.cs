using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Resources;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Genetics;

public interface IGeneProvider

{
    EitherAsync<Error, PrepareGeneResponse> ProvideGene(GeneIdentifier geneIdentifier, Func<string, int,Task<Unit>> reportProgress, CancellationToken cancel);

    EitherAsync<Error, Option<string>> GetGeneSetParent(GeneSetIdentifier genesetIdentifier,
        Func<string, int, Task<Unit>> reportProgress, CancellationToken cancellationToken);
}