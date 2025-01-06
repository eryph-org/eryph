using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.VmAgent;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using SimpleInjector;

using static LanguageExt.Prelude;
using static LanguageExt.Seq;

namespace Eryph.Modules.VmHostAgent.Inventory;

public sealed class DiskStoreChangeWatcherService(
    IBus bus,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    Container container)
    : IHostedService, IDisposable
{
    private IDisposable? _disposable;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        
        // TODO monitor changes to VMHostAgentConfiguration
        var vmHostAgentConfig = await GetConfig();
        var paths = append(
            vmHostAgentConfig.Environments.ToSeq()
                .SelectMany(e => e.Datastores.ToSeq().Map(ds => ds.Path))
                .ToSeq(),
            vmHostAgentConfig.Environments.ToSeq()
                .Map(e => e.Defaults.Volumes),
            vmHostAgentConfig.Datastores.ToSeq().Map(ds => ds.Path),
            Seq1(vmHostAgentConfig.Defaults.Volumes));


        _disposable = ObserveStores(paths).Subscribe();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _disposable?.Dispose();

        return Task.CompletedTask;
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
        _disposable?.Dispose();
    }

    private IObservable<System.Reactive.Unit> ObserveStores(Seq<string> paths) =>
        paths.ToObservable()
            .SelectMany(p => ObserveFolders(p).Merge(ObserveFiles(p)))
            .Throttle(TimeSpan.FromSeconds(1))
            .Select(_ => Observable.FromAsync(() => bus.SendLocal(new DiskStoreChangedEvent())))
            .Concat();

    private IObservable<FileSystemEventArgs> ObserveFolders(string path) =>
        Observable.Defer(() =>
            {
                FileSystemWatcher fsw = new(path)
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.DirectoryName,
                };
                return Observable.Return(fsw);
            })
            .SelectMany(fsw => Observable.Merge(
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        h => fsw.Created += h, h => fsw.Created -= h),
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        h => fsw.Deleted += h, h => fsw.Deleted -= h),
                    Observable.FromEventPattern<RenamedEventHandler, FileSystemEventArgs>(
                        h => fsw.Renamed += h, h => fsw.Renamed -= h))
                .Select(ep => ep.EventArgs)
                .Finally(fsw.Dispose));

    private IObservable<FileSystemEventArgs> ObserveFiles(string path) =>
        Observable.Defer(() =>
            {
                FileSystemWatcher fsw = new(path)
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName,
                    Filter = "*.vhdx",
                };
                return Observable.Return(fsw);
            })
            .SelectMany(fsw => Observable.Merge(
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        h => fsw.Created += h, h => fsw.Created -= h),
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        h => fsw.Deleted += h, h => fsw.Deleted -= h),
                    Observable.FromEventPattern<RenamedEventHandler, FileSystemEventArgs>(
                        h => fsw.Renamed += h, h => fsw.Renamed -= h))
                .Select(ep => ep.EventArgs)
                .Finally(fsw.Dispose));
}
