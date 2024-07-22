using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Genes.Commands;
using LanguageExt;
using LanguageExt.Common;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal class GeneRequestRegistry : IGeneRequestDispatcher
{
    //private readonly ITaskMessaging _messaging;
    private readonly IGeneRequestBackgroundQueue _queue;
    private readonly Container _container;
    private readonly IGeneProvider _geneProvider;

    private record ListeningTask(object Context,
        Func<ITaskMessaging, object, Either<Error, PrepareGeneResponse>, Task<Unit>> CompleteCallback,
        Func<object, IOperationTaskMessage> TaskMessageCallback);

    private record GenomeContext(IOperationTaskMessage Message, string OriginalRequest, Arr<string> ResolvedGenSets);

    public GeneRequestRegistry(IGeneRequestBackgroundQueue queue, Container container,  IGeneProvider geneProvider)
    {
        _queue = queue;
        _container = container;
        _geneProvider = geneProvider;
    }

    private readonly Atom<HashMap<GeneIdentifierWithType, Arr<ListeningTask>>> _pendingRequests =
        Prelude.Atom(HashMap<GeneIdentifierWithType, Arr<ListeningTask>>.Empty);

    private readonly Atom<HashMap<string, Arr<GeneIdentifierWithType>>> _pendingGenes =
        Prelude.Atom(HashMap<string, Arr<GeneIdentifierWithType>>.Empty);

    public async ValueTask NewGeneRequestTask(IOperationTaskMessage message, GeneType geneType, string geneIdentifier)
    {

        await NewGeneRequestTaskInternal(geneType, GeneIdentifier.New(geneIdentifier), message, 
            (m, ctx, r) => 
            r.ToAsync().FailOrComplete(m, (IOperationTaskMessage )ctx),
            ctx => (IOperationTaskMessage)ctx);
    }

    private async ValueTask NewGeneRequestTaskInternal(GeneType geneType, GeneIdentifier geneIdentifier, object context,
        Func<ITaskMessaging, object, Either<Error, PrepareGeneResponse>, Task<Unit>> completeCallback,
        Func<object, IOperationTaskMessage> taskMessageCallback)
    {
        var queueTask = false;
        var geneIdAndType = new GeneIdentifierWithType(geneType, geneIdentifier);

        // the pending requests are used to send messages to all listeners and to complete the task once done
        _pendingRequests.Swap(queue => queue.AddOrUpdate(geneIdAndType,
            Some: td => td.Add(new ListeningTask(context, completeCallback, taskMessageCallback)),
            None: () =>
            {
                _pendingGenes.Swap(pendingGenes => pendingGenes.AddOrUpdate(geneIdAndType.GeneIdentifier.GeneSet.ValueWithoutTag,
                    Some: genes => genes.Add(geneIdAndType),
                    None: () =>
                    {
                        queueTask = true;
                        return new Arr<GeneIdentifierWithType>(new[] { geneIdAndType });
                    })
                );

                return new Arr<ListeningTask>(new[] { new ListeningTask(context, completeCallback, taskMessageCallback) });
            }));

        // only queue a new task if it was not already queued (in that case we have added only a new listener)
        if (queueTask)
        {
            await _queue.QueueBackgroundWorkItemAsync(token => ProvideGene(geneIdAndType, token));
        }
    }

    private async ValueTask ProvideGene(GeneIdentifierWithType geneIdWithType, CancellationToken cancel)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(_container);

        var taskMessaging = scope.GetInstance<ITaskMessaging>();
        try
        {
            var result = await _geneProvider.ProvideGene(
                geneIdWithType.GeneType,
                geneIdWithType.GeneIdentifier,
                (message, progress) => ReportProgress(taskMessaging, geneIdWithType, message, progress), cancel);
            await EndRequest(taskMessaging, geneIdWithType, result);
        }
        catch (Exception ex)
        {
            await EndRequest(taskMessaging, geneIdWithType, Error.New(ex));
        }
    }

    private async Task<Unit> ReportProgress(ITaskMessaging taskMessaging, GeneIdentifierWithType geneIdWithType, string message, int progress)
    {

        await _pendingRequests.Value.Find(geneIdWithType).IfSomeAsync(async listening =>
        {
            foreach (var task in listening)
            {
                await taskMessaging.ProgressMessage(task.TaskMessageCallback(task.Context), new { message, progress });
            }

            return Unit.Default;
        });


        
        return Unit.Default;
    }

    private Task EndRequest(ITaskMessaging taskMessaging, GeneIdentifierWithType geneIdWithType, Either<Error, PrepareGeneResponse> result)
    {
        return from requestUpdate in _pendingRequests.Value.Find(geneIdWithType).IfSomeAsync(async listening =>
        {
            _pendingRequests.Swap(requests => requests.Remove(geneIdWithType));
            foreach (var task in listening)
            {
                await task.CompleteCallback(taskMessaging, task.Context, result);
            }
        })
        from nextQueued in _pendingGenes.Value.Find(geneIdWithType.GeneIdentifier.GeneSet.ValueWithoutTag).IfSomeAsync(async waitingGenes =>
        {
            var next = waitingGenes.HeadOrNone();
            
            _pendingGenes.Swap(genes =>
            {
                //remove pending gene from waiting list
                return next.Match(
                    Some: n => genes.AddOrUpdate(geneIdWithType.GeneIdentifier.GeneSet.ValueWithoutTag,
                        w => w.Remove(n),
                        () => waitingGenes),
                    None: () => genes.Remove(geneIdWithType.GeneIdentifier.GeneSet.ValueWithoutTag)
                );
            });

            // queue next gene of geneset
            await next.IfSomeAsync(async n => await _queue.QueueBackgroundWorkItemAsync(token => ProvideGene(n, token)));

        })
            select Unit.Default;
    }
}