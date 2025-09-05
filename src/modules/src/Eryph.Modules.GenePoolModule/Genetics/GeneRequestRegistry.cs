using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.GenePool.Genetics;

internal sealed class GeneRequestRegistry(
    Container container,
    ILogger logger)
    : IGeneRequestRegistry
{
    /// <summary>
    /// This semaphore is used to block the reader of the queue until
    /// a new message is available.
    /// </summary>
    private readonly SemaphoreSlim _availableSemaphore = new(0);
    
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Queue<(UniqueGeneIdentifier Id, GeneHash Hash)> _queue = new();
    private readonly Dictionary<(UniqueGeneIdentifier Id, GeneHash Hash), ISet<IOperationTaskMessage>> _pendingTasks = new();

    public async Task EnqueueGeneRequest(
        OperationTask<PrepareGeneCommand> task,
        CancellationToken cancellationToken)
    {
        var geneInfo = (task.Command.Id, task.Command.Hash);

        int position;
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_pendingTasks.TryGetValue(geneInfo, out var geneTasks))
            {
                // We already know about this gene. If it is not in the queue
                // (IndexOf() returns -1), it should currently be processed.
                position = _queue.ToList().IndexOf(geneInfo) + 1;
                geneTasks.Add(task);
                logger.LogDebug("Registering additional task {TaskId}. The gene {GeneId} ({GeneHash}) is already queued at position {Position}.",
                    task.TaskId, task.Command.Id, task.Command.Hash, position);
            }
            else
            {
                geneTasks = new HashSet<IOperationTaskMessage>(OperationTaskMessageEqualityComparer.Default);
                _pendingTasks.Add(geneInfo, geneTasks);
                geneTasks.Add(task);
                
                _queue.Enqueue(geneInfo);
                position = _queue.Count;
                // Increase the semaphore by one as we have enqueued a new request.
                _availableSemaphore.Release();
                logger.LogDebug("Adding gene {GeneId} ({GeneHash}) to the queue at position {Position} for task {TaskId}.",
                    task.Command.Id, task.Command.Hash, position, task.TaskId);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var taskMessaging = scope.GetInstance<ITaskMessaging>();
        var message = position > 0 ? $"Waiting for {position} other task(s)..." : "Waiting for next update...";
        await taskMessaging.ProgressMessage(task, new { message, progress = 0 });
    }

    public async Task<(UniqueGeneIdentifier Id, GeneHash Hash)> DequeueGeneRequest(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            // This semaphore will block until a new request has been enqueued.
            await _availableSemaphore.WaitAsync(cancellationToken);
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                // At this point, we should always be able to dequeue a request.
                // Even if the queue is empty for whatever reason, nothing bad
                // will happen. We will just continue to decrement the semaphore
                // until we block.
                if (_queue.TryDequeue(out var command))
                {
                    logger.LogDebug("Dequeuing request for gene {GeneId} ({GeneHash}).", command.Id, command.Hash);
                    return command;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    public async Task ReportProgress(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        string message,
        int progress)
    {
        List<IOperationTaskMessage> tasksToUpdate = [];
        await _semaphore.WaitAsync();
        try
        {
            if (_pendingTasks.TryGetValue((uniqueGeneId, geneHash), out var geneTasks))
                tasksToUpdate = [..geneTasks];
        }
        finally
        {
            _semaphore.Release();
        }

        if (tasksToUpdate.Count == 0)
            return;

        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var taskMessaging = scope.GetInstance<ITaskMessaging>();
        foreach (var task in tasksToUpdate)
        {
            await taskMessaging.ProgressMessage(
                task.OperationId,
                task.TaskId,
                new { message, progress });
        }
    }

    public async Task CompleteRequest(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        LanguageExt.Fin<PrepareGeneResponse> result)
    {
        List<IOperationTaskMessage> tasksToComplete = [];
        List<List<IOperationTaskMessage>> tasksToUpdate = [];
        await _semaphore.WaitAsync();
        try
        {
            if (_pendingTasks.TryGetValue((uniqueGeneId, geneHash), out var geneTasks))
                tasksToComplete = [..geneTasks];

            _pendingTasks.Remove((uniqueGeneId, geneHash));

            foreach (var gene in _queue.Skip(1))
            {
                _pendingTasks.TryGetValue(gene, out geneTasks);
                tasksToUpdate.Add(geneTasks?.ToList() ?? []);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var taskMessaging = scope.GetInstance<ITaskMessaging>();

        foreach (var task in tasksToComplete)
        {
            logger.LogDebug("Sending result to task {TaskId} for gene {GeneId} ({GeneHash}).",
                task.TaskId, uniqueGeneId, geneHash);
            await result.FailOrComplete(taskMessaging, task);
        }

        // Update the number of pending tasks for the remaining tasks in the queue.
        // We skip the first entry in the queue as it will be picked up for processing.
        foreach (var (tasks, index) in tasksToUpdate.Select((t, i) => (t, i)))
        {
            var message = $"Waiting for {index + 1} other task(s)...";
            foreach(var task in tasks)
            {
                await taskMessaging.ProgressMessage(task, new { message, progress = 0 });
            }
        }
    }

    private sealed class OperationTaskMessageEqualityComparer : IEqualityComparer<IOperationTaskMessage>
    {
        public static OperationTaskMessageEqualityComparer Default { get; } = new();

        public bool Equals(IOperationTaskMessage? x, IOperationTaskMessage? y) =>
            ReferenceEquals(x, y) || x?.OperationId == y?.OperationId && x?.TaskId == y?.TaskId;
        
        public int GetHashCode(IOperationTaskMessage obj) =>
            HashCode.Combine(obj.OperationId, obj.TaskId);
    }
}
