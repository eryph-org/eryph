using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using LanguageExt;
using LanguageExt.Common;
using SimpleInjector;
using SimpleInjector.Lifestyles;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

internal class GeneRequestRegistry(
    IGeneRequestBackgroundQueue queue,
    Container container)
    : IGeneRequestDispatcher
{
    private sealed record ListeningTask(IOperationTaskMessage Context);

    private readonly AtomHashMap<UniqueGeneIdentifier, Arr<ListeningTask>> _pendingRequests =
        AtomHashMap<UniqueGeneIdentifier, Arr<ListeningTask>>();

    public async ValueTask NewGeneRequestTask(
        IOperationTaskMessage message,
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash)
    {
        var queueTask = false;

        // the pending requests are used to send messages to all listeners and to complete the task once done
        _pendingRequests.SwapKey(uniqueGeneId, tasks => tasks.Match(
            Some: td =>
            {
                queueTask = false;
                return td.Add(new ListeningTask(message));
            },
            None: () =>
            {
                queueTask = true;
                return Array(new ListeningTask(message));
            }));

        // only queue a new task if it was not already queued (in that case we have added only a new listener)
        if (queueTask)
        {
            await queue.QueueBackgroundWorkItemAsync(token => ProvideGene(uniqueGeneId, geneHash, token));
        }
    }

    private async ValueTask ProvideGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancel)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);

        var taskMessaging = scope.GetInstance<ITaskMessaging>();
        var geneProvider = scope.GetInstance<IGeneProvider>();
        try
        {
            var result = await geneProvider.ProvideGene(
                uniqueGeneId,
                geneHash,
                (message, progress) => ReportProgress(taskMessaging, uniqueGeneId, message, progress),
                cancel);
            await EndRequest(taskMessaging, uniqueGeneId, result);
        }
        catch (Exception ex)
        {
            await EndRequest(taskMessaging, uniqueGeneId, Error.New(ex));
        }
    }

    private Task<Unit> ReportProgress(
        ITaskMessaging taskMessaging,
        UniqueGeneIdentifier uniqueGeneId,
        string message,
        int progress) =>
        _pendingRequests.Find(uniqueGeneId).IfSomeAsync(async listening =>
        {
            foreach (var task in listening)
            {
                await taskMessaging.ProgressMessage(task.Context, new { message, progress });
            }
        });

    private async Task EndRequest(
        ITaskMessaging taskMessaging,
        UniqueGeneIdentifier uniqueGeneId,
        Either<Error, PrepareGeneResponse> result)
    {
        var pending = _pendingRequests.Find(uniqueGeneId);
        _pendingRequests.Remove(uniqueGeneId);
        await pending.IfSomeAsync(async listening =>
        {
            _pendingRequests.Swap(requests => requests.Remove(uniqueGeneId));
            foreach (var task in listening)
            {
                await result.ToAsync().FailOrComplete(taskMessaging, task.Context);
            }
        });
    }
}
