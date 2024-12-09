using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Wmi;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiMsvmUtils;
using static Eryph.VmManagement.Wmi.WmiEventUtils;

namespace Eryph.Modules.VmHostAgent.Inventory;

/// <summary>
/// This service observes WMI events which are raised when the state of a
/// Hyper-V VM changes (e.g. when the VM is started or stopped).
/// When a proper event is observes, it raises a <see cref="VirtualMachineStateChangedEvent"/>.
/// This service acts separately from <see cref="VmChangeWatcherService"/>
/// as the state change event is time-sensitive. It triggers
/// <see cref="Networks.OVS.SyncPortOVSPortsEventHandler"/> which is required
/// for network connectivity of the VM.
/// </summary>
internal class VmStateChangeWatcherService(IBus bus, ILogger log)
    : WmiWatcherService(bus, log,
        new ManagementScope(@"root\virtualization\v2"),
        new WqlEventQuery(
            "__InstanceModificationEvent",
            TimeSpan.FromSeconds(3),
            "TargetInstance ISA 'Msvm_ComputerSystem' and TargetInstance.EnabledState <> PreviousInstance.EnabledState"))
{
    protected override Aff<Option<object>> OnEventArrived(ManagementBaseObject wmiEvent) =>
        from convertedEvent in ConvertEvent(
            wmiEvent,
            Seq("__CLASS", "Name", "EnabledState", "OtherEnabledState", "HealthState", "OnTimeInMilliseconds"))
        from message in OnEventArrived(convertedEvent)
        select message.Map(m => (object)m);

    internal static Aff<Option<VirtualMachineStateChangedEvent>> OnEventArrived(
        WmiEvent wmiEvent) =>
        from _ in SuccessAff(unit)
        let targetInstance = wmiEvent.TargetInstance
        from vmId in getVmId(targetInstance)
        from message in vmId
            .Map(id => CreateMessage(id, wmiEvent))
            .Sequence()
        select message;

    private static Aff<VirtualMachineStateChangedEvent> CreateMessage(
        Guid vmId,
        WmiEvent wmiEvent) =>
        from vmState in getVmState(wmiEvent.TargetInstance)
        from upTime in getVmUpTime(wmiEvent.TargetInstance)
        let message = new VirtualMachineStateChangedEvent
        {
            VmId = vmId,
            State = vmState.ToNullable(),
            UpTime = upTime,
            Timestamp = wmiEvent.Created,
        }
        select message;
}
