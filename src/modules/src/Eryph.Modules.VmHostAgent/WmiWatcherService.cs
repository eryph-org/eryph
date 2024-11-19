using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent;

internal abstract class WmiWatcherService : IHostedService, IDisposable
{
    private readonly ILogger _log;
    private readonly ManagementEventWatcher _watcher;

    protected WmiWatcherService(
        ILogger log,
        ManagementScope scope,
        EventQuery query)
    {
        _log = log;
        _watcher = new ManagementEventWatcher(scope, query);
        _watcher.EventArrived += OnEventArrived;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _watcher.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher.Stop();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected abstract Aff<Unit> OnEventArrived(ManagementBaseObject wmiEvent);

    private async void OnEventArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            _log.LogTrace("Processing WMI event: {Event}", e.NewEvent.GetText(TextFormat.Mof));
            _ = await OnEventArrived(e.NewEvent).RunUnit();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to process WMI event");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        _watcher.Dispose();
    }
}
