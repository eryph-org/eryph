using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.Inventory;

[UsedImplicitly]
internal class VirtualMachineRemovedEventHandler(
    IBus bus,
    ILogger logger,
    IPowershellEngine powershellEngine,
    WorkflowOptions workflowOptions)
    : IHandleMessages<VirtualMachineRemovedEvent>
{
    public async Task Handle(VirtualMachineRemovedEvent message)
    {
        var result = await Handle(message.VmId).Run();

        result.IfFail(e => { logger.LogError(e, "Failed to verify removal VM {VmId}", message.VmId); });
        if (result.IsFail)
            return;

        await result.ToOption().Flatten()
            .IfSomeAsync(e => bus.SendWorkflowEvent(workflowOptions, e));
    }

    private Aff<Option<CatletStateChangedEvent>> Handle(Guid vmId) =>
        from _ in SuccessAff(unit)
        let timestamp = DateTimeOffset.UtcNow
        // Verify that the VM is really missing
        from vmInfo in VmQueries.GetOptionalVmInfo(powershellEngine, vmId).ToAff()
        select vmInfo.Match<Option<CatletStateChangedEvent>>(
            Some: _ => None,
            None: () => new CatletStateChangedEvent
            {
                VmId = vmId,
                Status = VmStatus.Missing,
                UpTime = TimeSpan.Zero,
                Timestamp = timestamp,
            });
}
