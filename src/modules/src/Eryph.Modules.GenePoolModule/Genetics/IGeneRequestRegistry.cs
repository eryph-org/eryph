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
    
    Task ReportProgress(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        string message,
        int progress);
}
