using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Wmi;
using LanguageExt;
using LanguageExt.Common;
using Quartz;
using Rebus.Bus;
using SimpleInjector;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory;

internal class WmiVmUptimeCheckJob(Container container) : IJob
{
    private readonly IBus _bus = container.GetInstance<IBus>();
    private readonly WorkflowOptions _workflowOptions = container.GetInstance<WorkflowOptions>();

    public async Task Execute(IJobExecutionContext context)
    {
        // Uptime check only considers machines started within the last hour.
        // For longer running machines the inventory job takes care of updating uptime. 
        // The uptime only needs to be accurate during the early start phase to check if
        // the deployment has succeeded and to handle the removal of sensitive data from
        // the cloud-init configs.
        using var vmSearcher = new ManagementObjectSearcher(
            new ManagementScope(@"root\virtualization\v2"),
            new ObjectQuery("SELECT Name, EnabledState, OtherEnabledState, HealthState, OnTimeInMilliseconds "
                            + "FROM Msvm_ComputerSystem "
                            + "WHERE OnTimeInMilliseconds <> NULL AND OnTimeInMilliseconds < 3600000"));

        var timestamp = DateTimeOffset.UtcNow;
        using var collection = vmSearcher.Get();
        
        // TODO Dispose management objects properly
        var vms = collection.Cast<ManagementBaseObject>().ToList().ToSeq();
        _ = await SendMessages(vms, timestamp).RunUnit();
    }

    private Aff<Unit> SendMessages(
        Seq<ManagementBaseObject> vms,
        DateTimeOffset timestamp) =>
        from _ in vms.Map(vm  => SendMessage(vm, timestamp)).SequenceSerial()
        select unit;

    private Aff<Unit> SendMessage(
        ManagementBaseObject vm,
        DateTimeOffset timestamp) =>
        from vmId in WmiMsvmUtils.GetVmId(vm)
        from vmState in WmiMsvmUtils.GetVmState(vm)
        from upTime in WmiMsvmUtils.GetVmUpTime(vm)
        from _ in vmId
            .Map(id => new CatletStateChangedEvent
            {
                VmId = id,
                Status = InventoryConverter.MapVmInfoStatusToVmStatus(vmState),
                UpTime = upTime,
                Timestamp = timestamp,
            })
            .Map(message => Aff(async () =>
            {
                await _bus.SendWorkflowEvent(_workflowOptions, message);
                return unit;
            }))
            .Sequence()
        select unit;
}
