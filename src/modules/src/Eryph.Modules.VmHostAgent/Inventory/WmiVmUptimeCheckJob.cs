using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Events;
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
            new ObjectQuery("SELECT Name,OnTimeInMilliseconds FROM Msvm_ComputerSystem where OnTimeInMilliseconds <> NULL AND OnTimeInMilliseconds < 3600000"));

        using var collection = vmSearcher.Get();
        
        var vms = collection.Cast<ManagementBaseObject>().ToList().ToSeq();
        _ = await SendMessages(vms).RunUnit();
    }

    private Aff<Unit> SendMessages(Seq<ManagementBaseObject> vms) =>
        from _ in vms.Map(SendMessage).SequenceSerial()
        select unit;

    private Aff<Unit> SendMessage(ManagementBaseObject vm) =>
        from vmId in WmiMsvmUtils.GetVmId(vm)
        from uptimeMilliseconds in WmiUtils.GetPropertyValue<ulong>(vm, "OnTimeInMilliseconds")
        from uptime in Eff(() => TimeSpan.FromMilliseconds(uptimeMilliseconds))
            .MapFail(_ => Error.New($"The value {uptimeMilliseconds} is not a valid uptime."))
        from _ in vmId
            .Map(id => new CatletUpTimeChangedEvent
            {
                VmId = id,
                UpTime = uptime,
            })
            .Map(message => Aff(async () =>
            {
                await _bus.SendWorkflowEvent(_workflowOptions, message);
                return unit;
            }))
            .Sequence()
        select unit;
}
