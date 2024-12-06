using System; 
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Wmi;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiUtils;
using static Eryph.VmManagement.Wmi.WmiMsvmUtils;
using static Eryph.VmManagement.Wmi.WmiEventUtils;
using Eryph.VmManagement.Inventory;

namespace Eryph.Modules.VmHostAgent.Inventory;

/// <summary>
/// This service observes WMI events which are raised when Hyper-V VM is modified.
/// When a proper event is observes, it raises a <see cref="VirtualMachineChangedEvent"/>
/// which should trigger a new inventory of the VM.
/// </summary>
internal class VmChangeWatcherService(IBus bus, ILogger log)
    : WmiWatcherService(bus, log,
        new ManagementScope(@"root\virtualization\v2"),
        new WqlEventQuery(
            "__InstanceModificationEvent",
            TimeSpan.FromSeconds(1),
            "TargetInstance ISA 'Msvm_ComputerSystem' OR TargetInstance ISA 'Msvm_GuestNetworkAdapterConfiguration'"))
{
    protected override Aff<Option<object>> OnEventArrived(
        ManagementBaseObject wmiEvent) =>
        from convertedEvent in ConvertEvent(
            wmiEvent,
            Seq("__CLASS", "Name", "InstanceID", "OperationalStatus"))
        from message in OnEventArrived(convertedEvent)
        select message.Map(m => (object)m);

    internal static Aff<Option<VirtualMachineChangedEvent>> OnEventArrived(
        WmiEvent wmiEvent) =>
        from _ in SuccessAff(unit)
        let targetInstance = wmiEvent.TargetInstance
        from vmId in GetVmId(targetInstance)
        from message in vmId.Match(
            Some: id => OnEventArrived(wmiEvent, id),
            None: () => SuccessAff(Option<VirtualMachineChangedEvent>.None))
        select message;

    private static Aff<Option<VirtualMachineChangedEvent>> OnEventArrived(
        WmiEvent wmiEvent,
        Guid vmId) =>
        from _ in SuccessAff(unit)
        let targetInstance = wmiEvent.TargetInstance
        from className in getRequiredValue<string>(targetInstance, "__CLASS")
        from canBeInventoried in className == "Msvm_ComputerSystem"
            ? from state in getVmState(targetInstance)
              from operationalStatus in getOperationalStatus(targetInstance)
              // We check here if the VM can be inventoried based on the state information.
              // This way, we avoid raising too many events. The state information might change
              // rapidly while Hyper-V applies changes.
              let canBeInventoried = VmStateUtils.isInventorizable(state, operationalStatus)
              select canBeInventoried
            : SuccessEff(true)
        select canBeInventoried
            ? Some(new VirtualMachineChangedEvent { VmId = vmId })
            : None;
}
