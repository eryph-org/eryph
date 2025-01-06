using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using SimpleInjector;
using static LanguageExt.Prelude;
using static LanguageExt.Seq;

namespace Eryph.Modules.VmHostAgent.Inventory;

public sealed class DiskStoreChangeWatcherService(
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    Container container)
    : IHostedService, IDisposable
{
    private readonly IDictionary<string, DiskStoreWatcher> _watchers = new Dictionary<string, DiskStoreWatcher>();

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

        foreach (var path in paths)
        {
            var diskStoreWatcher = ActivatorUtilities.CreateInstance<DiskStoreWatcher>(
                container, path);
            _watchers.Add(path, diskStoreWatcher);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var kvp in _watchers.ToList())
        {
            kvp.Value.Dispose();
            _watchers.Remove(kvp.Key);
        }

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
        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }
    }
}

public sealed class DiskStoreWatcher : IDisposable
{
    private readonly IBus _bus;
    private readonly ILogger<DiskStoreWatcher> _logger;
    private readonly FileSystemWatcher _fileSystemWatcher;

    public DiskStoreWatcher(
        IBus bus,
        ILogger<DiskStoreWatcher> logger,
        string path)
    {
        _bus = bus;
        _logger = logger;
        _fileSystemWatcher = new FileSystemWatcher(path)
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = "*.vhdx",
        };
        _fileSystemWatcher.Renamed += OnChanged;
        _fileSystemWatcher.Created += OnChanged;
        _fileSystemWatcher.Deleted += OnChanged;
    }

    private async void OnChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            await _bus.SendLocal(new DiskStoreChangedEvent()
            {
                Path = _fileSystemWatcher.Path,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file system event");
        }
    }

    public void Dispose()
    {
        _fileSystemWatcher.Dispose();
    }
}
