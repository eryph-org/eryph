using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.VmAgent;
using LanguageExt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

using static LanguageExt.Prelude;
using static LanguageExt.Seq;

namespace Eryph.Modules.HostAgent.Inventory;

public sealed class DiskStoresChangeWatcherService(
    IBus bus,
    ILogger logger,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    InventoryConfig inventoryConfig)
    : IHostedService, IDisposable
{
    private IDisposable? _subscription;

    // The semaphore is not disposed as it can block waiting tasks forever
    // and block the application shutdown. As this service is a singleton,
    // the disposal is not required for resource cleanup.
    // See https://github.com/dotnet/runtime/issues/59639.
#pragma warning disable CA2213 // Disposable fields should be disposed
    private readonly SemaphoreSlim _semaphore = new(1, 1);
#pragma warning restore CA2213 // Disposable fields should be disposed
    
    private bool _stopping;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Restart();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Do not pass the cancellationToken as we must always wait
        // for the semaphore. Another thread might be restarting the
        // watchers at this moment. We must wait for that thread to
        // complete so we can stop the watchers correctly.
#pragma warning disable CA2016
        await _semaphore.WaitAsync();
#pragma warning restore CA2016
        try
        {
            _stopping = true;
            _subscription?.Dispose();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task Restart()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_stopping)
                return;

            logger.LogInformation("Starting watcher for disk stores with latest settings...");

            _subscription?.Dispose();

            var vmHostAgentConfig = await GetConfig();
            var paths = append(
                vmHostAgentConfig.Environments.ToSeq()
                    .Bind(e => e.Datastores.ToSeq())
                    .Filter(ds => ds.WatchFileSystem)
                    .Map(ds => ds.Path),
                vmHostAgentConfig.Environments.ToSeq()
                    .Filter(e => e.Defaults.WatchFileSystem)
                    .Map(e => e.Defaults.Volumes),
                vmHostAgentConfig.Datastores.ToSeq()
                    .Filter(ds => ds.WatchFileSystem)
                    .Map(ds => ds.Path),
                Seq1(vmHostAgentConfig.Defaults)
                    .Filter(d => d.WatchFileSystem)
                    .Map(d => d.Volumes));

            // The observable should not terminate unless we dispose it. When the observable
            // ends, we stop monitoring the file system events which would be a bug.
            _subscription = ObserveStores(paths).Subscribe(
                onNext: _ => { },
                onError: ex => logger.LogCritical(
                    ex, "Failed to monitor file system events for the disk stores. Inventory updates might be delayed until eryph is restarted."),
                onCompleted: () => logger.LogCritical(
                    "The monitoring of file system events for the disk stores stopped unexpectedly. Inventory updates might be delayed until eryph is restarted."));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<VmHostAgentConfiguration> GetConfig()
    {
        var result = await hostSettingsProvider.GetHostSettings()
            .Bind(vmHostAgentConfigManager.GetCurrentConfiguration)
            .ToAff(identity)
            .Run();

        return result.ThrowIfFail();
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    /// <summary>
    /// Creates an <see cref="IObservable{T}"/> which monitors the given
    /// <paramref name="paths"/>.
    /// </summary>
    /// <remarks>
    /// This method internally uses multiple <see cref="FileSystemWatcher"/>s
    /// to monitor the <paramref name="paths"/>. For simplicity, all their
    /// events are folded into a single event stream. The event stream is throttled
    /// to avoid triggering too many inventory actions. Every event, which emerges
    /// at the end, triggers a full inventory of all disk stores by raising a
    /// <see cref="DiskStoresChangedEvent"/> via the local Rebus.
    /// </remarks>
    private IObservable<System.Reactive.Unit> ObserveStores(Seq<string> paths) =>
        paths.ToObservable()
            .Select(ObservePath)
            .Merge()
            .Throttle(inventoryConfig.DiskEventDelay)
            .Select(_ => Observable.FromAsync(() => bus.SendLocal(new DiskStoresChangedEvent()))
                .Catch((Exception ex) =>
                {
                    logger.LogError(ex, "Could not send Rebus event for disk store change");
                    return Observable.Return(System.Reactive.Unit.Default);
                }))
            .Concat();

    private IObservable<FileSystemEventArgs> ObservePath(string path) =>
        Observable.Defer(() =>
        {
            try
            {
                // Proactively create the path if it does not exist. We cannot set up
                // the file system watcher otherwise.
                Directory.CreateDirectory(path);
                return Observable.Return(path);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "The store path '{Path}' cannot be created and will not be monitored.", path);
                return Observable.Empty<string>();
            }
        })
        .SelectMany(
            Observe(() => new FileSystemWatcher(path)
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.DirectoryName,
            }).Merge(Observe(() => new FileSystemWatcher(path)
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName,
                Filter = "*.vhdx",
            })))
        .SelectMany(fsw => Observable.Merge(
                Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                    h => fsw.Created += h, h => fsw.Created -= h),
                Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                    h => fsw.Deleted += h, h => fsw.Deleted -= h),
                Observable.FromEventPattern<RenamedEventHandler, FileSystemEventArgs>(
                    h => fsw.Renamed += h, h => fsw.Renamed -= h))
            .Select(ep => ep.EventArgs)
            .Finally(fsw.Dispose));

    /// <summary>
    /// Tries to create the <see cref="FileSystemWatcher"/>.
    /// </summary>
    /// <remarks>
    /// The constructor of <see cref="FileSystemWatcher"/> throws when the path is not accessible
    /// or does not exist.
    /// </remarks>
    private IObservable<FileSystemWatcher> Observe(Func<FileSystemWatcher> factory) =>
        Observable.Defer(() => Observable.Return(factory()))
            .Catch((Exception ex) =>
            {
                logger.LogWarning(ex,
                    "Failed to create file system watcher. The corresponding path will not be monitored.");
                return Observable.Empty<FileSystemWatcher>();
            });
}
