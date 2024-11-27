using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement;
using Eryph.VmManagement.Wmi;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiUtils;
using static Eryph.VmManagement.Wmi.WmiMsvmUtils;
using static Eryph.VmManagement.Wmi.WmiEventUtils;

namespace Eryph.Modules.VmHostAgent.Inventory;

/// <summary>
/// This service observes WMI events which are raised when the status of a
/// Hyper-V VM changes (e.g. when the VM is started or stopped).
/// When a proper event is observes, it raises a <see cref="VirtualMachineStateChangedEvent"/>.
/// This service acts separately from <see cref="VmChangeWatcherService"/>
/// as the state change event is time-sensitive. It triggers
/// <see cref="Networks.OVS.SyncPortOVSPortsEventHandler"/> which is required
/// for network connectivity of the VM.
/// </summary>
internal class VmStatusChangeWatcherService(
    IBus bus,
    ILogger log)
    : WmiWatcherService(log,
        new ManagementScope(@"root\virtualization\v2"),
        new WqlEventQuery(
            "__InstanceModificationEvent",
            TimeSpan.FromSeconds(3),
            "TargetInstance ISA 'Msvm_ComputerSystem' and TargetInstance.EnabledState <> PreviousInstance.EnabledState"))
{
    protected override Aff<Unit> OnEventArrived(ManagementBaseObject wmiEvent) =>
        from targetInstance in GetTargetInstance(wmiEvent)
        from creationTime in GetCreationTime(wmiEvent)
        from vmId in GetVmId(targetInstance)
        from _2 in vmId
            .Map(id => SendMessage(id, creationTime, targetInstance))
            .Sequence()
        select unit;

    private Aff<Unit> SendMessage(
        Guid vmId,
        DateTimeOffset creationTime,
        ManagementBaseObject targetInstance) =>
        from vmState in GetVmState(targetInstance)
        from upTime in GetVmUpTime(targetInstance)
        let message = new VirtualMachineStateChangedEvent
        {
            VmId = vmId,
            State = vmState,
            UpTime = upTime,
            Timestamp = creationTime,
        }
        from _ in Aff(async () =>
        {
            await bus.SendLocal(message);
            return unit;
        })
        select unit;
}
