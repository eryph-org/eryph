using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.GenePool.Genetics;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.GenePool;

[UsedImplicitly]
internal class PrepareGeneCommandHandler(
    IGeneRequestRegistry geneRequestRegistry)
    : IHandleMessages<OperationTask<PrepareGeneCommand>>
{
    public async Task Handle(OperationTask<PrepareGeneCommand> message)
    {
        await geneRequestRegistry.EnqueueGeneRequest(message, CancellationToken.None);
        // The task will be completed by the background process. Hence,
        // no FailOrComplete() is required here.
    }
}
