using System;
using System.Management;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiMsvmUtils;
using static Eryph.VmManagement.Wmi.WmiEventUtils;

namespace Eryph.Modules.HostAgent.Inventory;

/// <summary>
/// This service observes WMI events which are raised when a Hyper-V VM is deleted.
/// When a proper event is observes, it raises a <see cref="VirtualMachineRemovedEvent"/>.
/// </summary>
internal class VmRemovalWatcherService(IBus bus, ILogger log)
    : WmiWatcherService(bus, log,
        new ManagementScope(@"root\virtualization\v2"),
        new WqlEventQuery(
            "__InstanceDeletionEvent",
            TimeSpan.FromSeconds(3),
            "TargetInstance ISA 'Msvm_ComputerSystem'"))
{
    private readonly ILogger _log = log;

    protected override Aff<Option<object>> OnEventArrived(ManagementBaseObject wmiEvent) =>
        from convertedEvent in convertEvent(
            wmiEvent,
            Seq("__CLASS", "Name"))
        let targetInstance = convertedEvent.TargetInstance
        from vmId in getVmId(targetInstance)
        from _ in Eff(() =>
        {
            _log.LogDebug("Received deletion event for VM {VmId} at {Timestamp:O}",
                vmId, convertedEvent.Created);
            return unit;
        })
        select vmId.Map(id => (object)new VirtualMachineRemovedEvent { VmId = id });
}
