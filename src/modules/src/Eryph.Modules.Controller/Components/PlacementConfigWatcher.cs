using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.Rebus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Watches the operator-editable placement configuration file and asks the
/// controller to re-publish the <see cref="ConfigDomain.PlacementConfig"/> domain
/// whenever it changes. The refresh handler is idempotent (no-op when the content
/// is unchanged), so duplicate file-system events are harmless.
/// </summary>
internal sealed class PlacementConfigWatcher(
    PlacementConfigOptions options,
    IBus bus,
    ILogger<PlacementConfigWatcher> logger)
    : IHostedService, IDisposable
{
    private FileSystemWatcher? _watcher;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var path = options.ConfigPath;
        if (string.IsNullOrWhiteSpace(path))
            return Task.CompletedTask;

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
        {
            logger.LogDebug("Not watching placement config; directory {Directory} does not exist.", directory);
            return Task.CompletedTask;
        }

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;
        return Task.CompletedTask;
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => _ = TriggerRefreshAsync();

    private async Task TriggerRefreshAsync()
    {
        try
        {
            await bus.Advanced.Routing.Send(
                QueueNames.Controllers,
                new RefreshConfigDomainCommand { Domain = ConfigDomain.PlacementConfig });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to trigger placement configuration refresh.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _watcher = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _watcher?.Dispose();
}
