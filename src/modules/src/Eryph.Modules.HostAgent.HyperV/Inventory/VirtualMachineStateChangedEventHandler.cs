using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.Inventory;

[UsedImplicitly]
internal class VirtualMachineStateChangedEventHandler(
    IBus bus,
    WorkflowOptions workflowOptions)
    : IHandleMessages<VirtualMachineStateChangedEvent>
{
    public Task Handle(VirtualMachineStateChangedEvent message) =>
        bus.SendWorkflowEvent(workflowOptions, new CatletStateChangedEvent
        {
            VmId = message.VmId,
            Status = VmStateUtils.toVmStatus(Optional(message.State)),
            UpTime = message.UpTime,
            Timestamp = message.Timestamp,
        });
}
