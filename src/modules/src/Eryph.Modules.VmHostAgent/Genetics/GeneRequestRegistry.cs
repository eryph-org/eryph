using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Genes.Commands;
using LanguageExt;
using LanguageExt.Common;
using SimpleInjector;
using SimpleInjector.Lifestyles;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal class GeneRequestRegistry(
    IGeneRequestBackgroundQueue queue,
    Container container)
    : IGeneRequestDispatcher
{
    private sealed record ListeningTask(IOperationTaskMessage Context);

    private readonly AtomHashMap<GeneIdentifierWithType, Arr<ListeningTask>> _pendingRequests =
        AtomHashMap<GeneIdentifierWithType, Arr<ListeningTask>>();

    public async ValueTask NewGeneRequestTask(
        IOperationTaskMessage message,
        GeneIdentifierWithType geneIdWithType)
    {
        var queueTask = false;

        // the pending requests are used to send messages to all listeners and to complete the task once done
        _pendingRequests.SwapKey(geneIdWithType, tasks => tasks.Match(
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
            await queue.QueueBackgroundWorkItemAsync(token => ProvideGene(geneIdWithType, token));
        }
    }

    private async ValueTask ProvideGene(GeneIdentifierWithType geneIdWithType, CancellationToken cancel)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);

        var taskMessaging = scope.GetInstance<ITaskMessaging>();
        var geneProvider = scope.GetInstance<IGeneProvider>();
        try
        {
            var result = await geneProvider.ProvideGene(
                geneIdWithType.GeneType,
                geneIdWithType.GeneIdentifier,
                (message, progress) => ReportProgress(taskMessaging, geneIdWithType, message, progress),
                cancel);
            await EndRequest(taskMessaging, geneIdWithType, result);
        }
        catch (Exception ex)
        {
            await EndRequest(taskMessaging, geneIdWithType, Error.New(ex));
        }
    }

    private Task<Unit> ReportProgress(
        ITaskMessaging taskMessaging,
        GeneIdentifierWithType geneIdWithType,
        string message,
        int progress) =>
        _pendingRequests.Find(geneIdWithType).IfSomeAsync(async listening =>
        {
            foreach (var task in listening)
            {
                await taskMessaging.ProgressMessage(task.Context, new { message, progress });
            }
        });

    private async Task EndRequest(
        ITaskMessaging taskMessaging,
        GeneIdentifierWithType geneIdWithType,
        Either<Error, PrepareGeneResponse> result)
    {
        var pending = _pendingRequests.Find(geneIdWithType);
        _pendingRequests.Remove(geneIdWithType);
        await pending.IfSomeAsync(async listening =>
        {
            _pendingRequests.Swap(requests => requests.Remove(geneIdWithType));
            foreach (var task in listening)
            {
                await result.ToAsync().FailOrComplete(taskMessaging, task.Context);
            }
        });
    }
}
