using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.VmManagement;
using Eryph.VmManagement.Data;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Transport;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory;

internal sealed class WmiWatcherModuleService : IHostedService, IDisposable
{
    private readonly IBus _bus;
    private readonly ILogger _log;
    private readonly WorkflowOptions _workflowOptions;

    private readonly Timer _upTimeTimer;

    private const int UpTimeCheckSeconds= 60;

    public WmiWatcherModuleService(IBus bus, ILogger log, WorkflowOptions workflowOptions)
    {
        _bus = bus;
        _log = log;
        _workflowOptions = workflowOptions;

        _upTimeTimer = new Timer(UpTimeCheck, null, Timeout.Infinite, 0);
    }


    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartWatching();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {

        _upTimeTimer.Change(Timeout.Infinite, 0);
        _upTimeTimer.Dispose();
        return Task.CompletedTask;
    }

    public void StartWatching()
    {
        _log.LogDebug("Starting WMI Watcher");

        _upTimeTimer.Change(TimeSpan.FromSeconds(UpTimeCheckSeconds), TimeSpan.Zero);
    }

    private void UpTimeCheck(object? state)
    {
        try
        {
            _upTimeTimer.Change(Timeout.Infinite, 0);

            // Uptime check only considers machines started within the last hour.
            // For longer running machines the inventory job takes care of updating uptime. 
            // It has to be only accurate during early start phase to check if deployment has
            // succeeded and to handle the removal of sensitive data from cloud-init configs.
            using var vmSearcher = new ManagementObjectSearcher(
                new ManagementScope(@"root\virtualization\v2"),
                new ObjectQuery("SELECT Name,OnTimeInMilliseconds FROM MSVM_ComputerSystem where OnTimeInMilliseconds <> NULL AND OnTimeInMilliseconds < 3600000"));

            using var vms = vmSearcher.Get();

            foreach (var vm in vms)
            {
                var vmId = GetVmId(vm);

                var upTimeInMilliseconds = (ulong)vm.GetPropertyValue("OnTimeInMilliseconds");

                _bus.SendWorkflowEvent(_workflowOptions, new CatletUpTimeChangedEvent()
                {
                    VmId = vmId.ValueUnsafe(),
                    UpTime = TimeSpan.FromMilliseconds(upTimeInMilliseconds)
                });
            }
        }
        finally
        {
            _upTimeTimer.Change(TimeSpan.FromSeconds(UpTimeCheckSeconds), TimeSpan.Zero);
        }
    }


    private static Option<Guid> GetVmId(ManagementBaseObject targetInstance) =>
        from vmId in targetInstance.ClassPath.ClassName switch
        {
            "Msvm_ComputerSystem" =>
                // Msvm_ComputerSystem can be either the host or a VM. For VMs, the name
                // contains the Guid which identifies the VM.
                from vmId in parseGuid(targetInstance["Name"] as string)
                select vmId,
            _ =>
                from _ in Some(unit)
                let instanceId = (string)targetInstance["InstanceID"]
                let parts = instanceId.Split('\\')
                from idPart in parts.Length == 3 ? Some(parts[1]) : None
                from vmId in parseGuid(idPart)
                select vmId
        }
        select vmId;

    public void Dispose()
    {
        _upTimeTimer?.Dispose();
    }
}
