using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class VirtualMachineStartHandler(
    ITaskMessaging messaging,
    IPowershellEngine engine)
    : VirtualMachineStateTransitionHandler<StartCatletVMCommand>(messaging, engine)
{
    protected override PsCommandBuilder CreateTransitionCommand(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        PsCommandBuilder.Create()
            .AddCommand("Start-VM")
            .AddParameter("VM", vmInfo.PsObject);
}
