using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class VirtualMachineStopHandler(ITaskMessaging messaging, IPowershellEngine engine)
    : VirtualMachineStateTransitionHandler<StopVMCommand>(messaging, engine)
{
    protected override PsCommandBuilder CreateTransitionCommand(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        PsCommandBuilder.Create()
            .AddCommand("Stop-VM")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("TurnOff");
}
