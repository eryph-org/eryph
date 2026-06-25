using System.Collections.Concurrent;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.Sys;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.GenePool.Genetics;
using LanguageExt;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using static LanguageExt.Prelude;
using GenesetTagManifestData = Eryph.GenePool.Model.GenesetTagManifestData;

namespace Eryph.Modules.GenePoolModule.Test.Genetics;

public class GeneticsRequestWatcherServiceTests
{
    private static readonly GeneHash GeneHash1 = GeneHash.New(
        "sha256:a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c");

    private static readonly GeneHash GeneHash2 = GeneHash.New(
        "sha256:b8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c");

    private static readonly UniqueGeneIdentifier Gene1 = new(
        GeneType.Volume,
        GeneIdentifier.New("gene:acme/acme-os/1.0:sda"),
        Architecture.New("hyperv/amd64"));

    private static readonly UniqueGeneIdentifier Gene2 = new(
        GeneType.Volume,
        GeneIdentifier.New("gene:acme/acme-os/1.0:sdb"),
        Architecture.New("hyperv/amd64"));

    /// <summary>
    /// Regression test for https://github.com/eryph-org/eryph/issues/367.
    /// A long-running download for one gene must not block the download of a
    /// different gene. Both fake downloads block until released; if the watcher
    /// processed requests serially, the second gene would never start and the
    /// test would time out.
    /// </summary>
    [Fact]
    public async Task Requests_for_different_genes_are_processed_concurrently()
    {
        await using var container = CreateContainer();
        var provider = new BlockingGeneProvider();
        provider.RegisterGene(Gene1);
        provider.RegisterGene(Gene2);
        container.RegisterInstance<IGeneProvider>(provider);

        var registry = new GeneRequestRegistry(container, new TaskCancellationRegistry(), NullLogger.Instance);
        var service = new GeneticsRequestWatcherService(container, registry, NullLogger.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await registry.EnqueueGeneRequest(CreateTask(Gene1, GeneHash1), CancellationToken.None);
            await registry.EnqueueGeneRequest(CreateTask(Gene2, GeneHash2), CancellationToken.None);

            var bothStarted = Task.WhenAll(
                provider.Started(Gene1),
                provider.Started(Gene2));
            var finished = await Task.WhenAny(bothStarted, Task.Delay(TimeSpan.FromSeconds(10)));

            finished.Should().BeSameAs(bothStarted,
                "a download for one gene must not block the download of a different gene");
            provider.MaxConcurrency.Should().BeGreaterThanOrEqualTo(2);
        }
        finally
        {
            // Unblock the workers so the background service can shut down, and make
            // sure it actually stops (workers must observe cancellation and exit).
            provider.ReleaseAll();
            var stopped = service.StopAsync(CancellationToken.None);
            (await Task.WhenAny(stopped, Task.Delay(TimeSpan.FromSeconds(10))))
                .Should().BeSameAs(stopped, "the background service should shut down its workers cleanly");
        }
    }

    /// <summary>
    /// Cancelling one of several tasks waiting for the same (deduplicated) download
    /// reports just that task as cancelled and lets the shared download continue for
    /// the others; cancelling the last waiter stops the download.
    /// </summary>
    [Fact]
    public async Task Cancelling_tasks_reports_them_and_stops_the_download_when_none_remain()
    {
        var cancelledTaskIds = new ConcurrentDictionary<Guid, TaskCompletionSource>();
        var taskMessaging = new Mock<ITaskMessaging>();
        taskMessaging
            .Setup(m => m.CancelTask(It.IsAny<IOperationTaskMessage>(), It.IsAny<IDictionary<string, string>?>()))
            .Returns((IOperationTaskMessage message, IDictionary<string, string>? _) =>
            {
                cancelledTaskIds.GetOrAdd(message.TaskId, _ => new TaskCompletionSource()).TrySetResult();
                return Task.CompletedTask;
            });

        await using var container = CreateContainer(taskMessaging.Object);
        var provider = new CancellableGeneProvider();
        container.RegisterInstance<IGeneProvider>(provider);

        var cancellationRegistry = new TaskCancellationRegistry();
        var registry = new GeneRequestRegistry(container, cancellationRegistry, NullLogger.Instance);
        var service = new GeneticsRequestWatcherService(container, registry, NullLogger.Instance);

        var task1 = CreateTask(Gene1, GeneHash1);
        var task2 = CreateTask(Gene1, GeneHash1);

        await service.StartAsync(CancellationToken.None);
        try
        {
            // Both tasks wait for the same gene -> a single (deduplicated) download.
            await registry.EnqueueGeneRequest(task1, CancellationToken.None);
            await registry.EnqueueGeneRequest(task2, CancellationToken.None);
            (await Task.WhenAny(provider.Started, Task.Delay(TimeSpan.FromSeconds(10))))
                .Should().BeSameAs(provider.Started, "the shared download should start");

            // Cancel the first task: it must be reported as cancelled, but the download
            // continues because the second task still wants the gene.
            cancellationRegistry.Cancel(task1.OperationId);
            await WaitFor(cancelledTaskIds, task1.TaskId);
            provider.Cancelled.IsCompleted.Should().BeFalse("the download is still wanted by the second task");

            // Cancel the second task: now nobody wants the gene, so the download stops.
            cancellationRegistry.Cancel(task2.OperationId);
            await WaitFor(cancelledTaskIds, task2.TaskId);
            (await Task.WhenAny(provider.Cancelled, Task.Delay(TimeSpan.FromSeconds(10))))
                .Should().BeSameAs(provider.Cancelled, "the download should be cancelled once no task wants it");
        }
        finally
        {
            provider.Release();
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitFor(ConcurrentDictionary<Guid, TaskCompletionSource> signals, Guid taskId)
    {
        var signal = signals.GetOrAdd(taskId, _ => new TaskCompletionSource()).Task;
        (await Task.WhenAny(signal, Task.Delay(TimeSpan.FromSeconds(10))))
            .Should().BeSameAs(signal, $"task {taskId} should have been reported as cancelled");
    }

    private static Container CreateContainer(ITaskMessaging? taskMessaging = null)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        // The registry resolves ITaskMessaging from a scope to report progress
        // and completion. A loose mock returns completed tasks for these calls.
        container.RegisterInstance(taskMessaging ?? new Mock<ITaskMessaging>().Object);
        return container;
    }

    private static OperationTask<PrepareGeneCommand> CreateTask(
        UniqueGeneIdentifier geneId,
        GeneHash geneHash) =>
        new(
            new PrepareGeneCommand
            {
                Id = geneId,
                Hash = geneHash,
                AgentName = "test",
            },
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

    /// <summary>
    /// A fake gene provider whose downloads block until <see cref="ReleaseAll"/>
    /// is called. It records the maximum number of downloads running at the same
    /// time so the test can assert that requests are processed concurrently.
    /// </summary>
    private sealed class BlockingGeneProvider : IGeneProvider
    {
        private readonly TaskCompletionSource _release = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource> _started = new();
        private int _current;
        private int _maxConcurrency;

        public int MaxConcurrency => Volatile.Read(ref _maxConcurrency);

        public Aff<CancelRt, PrepareGeneResponse> ProvideGene(
            UniqueGeneIdentifier uniqueGeneId,
            GeneHash geneHash,
            Func<string, int, Task> reportProgress) =>
            Aff<CancelRt, PrepareGeneResponse>(async _ =>
            {
                var current = Interlocked.Increment(ref _current);
                UpdateMax(current);
                Gate(uniqueGeneId).TrySetResult();
                try
                {
                    await _release.Task.ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _current);
                }

                return new PrepareGeneResponse { RequestedGene = uniqueGeneId };
            });

        public Aff<CancelRt, string> GetGeneContent(
            UniqueGeneIdentifier uniqueGeneId,
            GeneHash geneHash) =>
            throw new NotSupportedException();

        public Aff<CancelRt, GenesetTagManifestData> GetGeneSetManifest(
            GeneSetIdentifier geneSetId) =>
            throw new NotSupportedException();

        public void RegisterGene(UniqueGeneIdentifier geneId) =>
            _started[geneId.Value] = new TaskCompletionSource();

        public Task Started(UniqueGeneIdentifier geneId) => Gate(geneId).Task;

        private TaskCompletionSource Gate(UniqueGeneIdentifier geneId) =>
            _started.GetOrAdd(geneId.Value, _ => new TaskCompletionSource());

        public void ReleaseAll() => _release.TrySetResult();

        private void UpdateMax(int current)
        {
            int observed;
            while (current > (observed = Volatile.Read(ref _maxConcurrency)))
                if (Interlocked.CompareExchange(ref _maxConcurrency, current, observed) == observed)
                    return;
        }
    }

    /// <summary>
    /// A fake gene provider whose download reports progress in a loop (driving the
    /// registry's cancellation pruning) and observes its cancellation token, so the
    /// test can assert that the download is stopped when it is cancelled.
    /// </summary>
    private sealed class CancellableGeneProvider : IGeneProvider
    {
        private readonly TaskCompletionSource _started = new();
        private readonly TaskCompletionSource _cancelled = new();
        private readonly TaskCompletionSource _release = new();

        public Task Started => _started.Task;
        public Task Cancelled => _cancelled.Task;

        public void Release() => _release.TrySetResult();

        public Aff<CancelRt, PrepareGeneResponse> ProvideGene(
            UniqueGeneIdentifier uniqueGeneId,
            GeneHash geneHash,
            Func<string, int, Task> reportProgress) =>
            Aff<CancelRt, PrepareGeneResponse>(async rt =>
            {
                _started.TrySetResult();
                try
                {
                    while (true)
                    {
                        rt.CancellationToken.ThrowIfCancellationRequested();
                        // Reporting progress runs the registry's pruning of cancelled tasks.
                        await reportProgress("downloading", 0).ConfigureAwait(false);
                        await Task.WhenAny(_release.Task, Task.Delay(25, rt.CancellationToken)).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    _cancelled.TrySetResult();
                    throw;
                }
            });

        public Aff<CancelRt, string> GetGeneContent(
            UniqueGeneIdentifier uniqueGeneId,
            GeneHash geneHash) =>
            throw new NotSupportedException();

        public Aff<CancelRt, GenesetTagManifestData> GetGeneSetManifest(
            GeneSetIdentifier geneSetId) =>
            throw new NotSupportedException();
    }
}
