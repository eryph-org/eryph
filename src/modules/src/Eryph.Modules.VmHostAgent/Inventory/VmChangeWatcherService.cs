using System; 
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using Eryph.VmManagement.Data;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiUtils;
using static Eryph.VmManagement.Wmi.WmiMsvmUtils;
using static Eryph.VmManagement.Wmi.WmiEventUtils;

namespace Eryph.Modules.VmHostAgent.Inventory;

/// <summary>
/// This service observes WMI events which are raised when Hyper-V VM is modified.
/// When a proper event is observes, it raises a <see cref="VirtualMachineChangedEvent"/>
/// which should trigger a new inventory of the VM.
/// </summary>
internal class VmChangeWatcherService(IBus bus, ILogger log)
    : WmiWatcherService(log,
        new ManagementScope(@"root\virtualization\v2"),
        new WqlEventQuery(
            "__InstanceModificationEvent",
            TimeSpan.FromSeconds(10),
            "TargetInstance ISA 'Msvm_ComputerSystem' OR TargetInstance ISA 'Msvm_GuestNetworkAdapterConfiguration'"))
{
    private readonly ILogger _log = log;

    protected override Aff<Unit> OnEventArrived(ManagementBaseObject wmiEvent) =>
        from targetInstance in GetTargetInstance(wmiEvent)
        // Skip the event when the VM is still being modified, i.e. the operational
        // status is InService. The inventory only needs to update when the change is
        // completed. Additionally, a lot of changes can complete rather quickly. Hence,
        // this check limits the number of events which are raised in short succession.
        // There is a separate watcher for the VM status which will propagate status
        // changes quicker.
        from skipEvent in targetInstance.ClassPath.ClassName == "Msvm_ComputerSystem"
            ? from operationalStatus in GetPropertyValue<ushort[]>(
                  targetInstance, "OperationalStatus")
              from firstStatus in operationalStatus.HeadOrNone()
                  .ToEff(Error.New("The operational status of the VM is invalid."))
              select firstStatus == (ushort)VMComputerSystemOperationalStatus.InService
            : SuccessEff(false)
        let ___ = fun(() =>
        {
            if (skipEvent)
                _log.LogWarning("Skipping event");
        })
        from vmId in GetVmId(targetInstance)
        from _2 in vmId.Filter(_ => !skipEvent)
            .Map(id => new VirtualMachineChangedEvent { VmId = id })
            .Map(m => Aff(async () =>
            {
                await bus.SendLocal(m);
                return unit;
            }))
            .Sequence()
        select unit;
}
