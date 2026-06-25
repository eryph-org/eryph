using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using LanguageExt;

namespace Eryph.Modules.GenePool.Genetics;

public interface IGeneRequestRegistry
{
    Task CompleteRequest(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Fin<PrepareGeneResponse> result);

    Task<(UniqueGeneIdentifier Id, GeneHash Hash)> DequeueGeneRequest(
        CancellationToken cancellationToken);

    Task EnqueueGeneRequest(
        OperationTask<PrepareGeneCommand> task,
        CancellationToken cancellationToken);

    /// <summary>
    /// Begins downloading the given gene and returns a token that is cancelled when the
    /// host stops or when all of the gene's waiting tasks have been cancelled. The
    /// returned token must be used for the download work.
    /// </summary>
    CancellationToken BeginDownload(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken hostToken);

    Task ReportProgress(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        string message,
        int progress);
}
