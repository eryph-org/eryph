using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using JetBrains.Annotations;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class VirtualMachineShutdownHandler : VirtualMachineStateTransitionHandler<ShutdownVMCommand>
{
    public VirtualMachineShutdownHandler(ITaskMessaging messaging, IPowershellEngine engine) : base(messaging, engine)
    {
    }

    protected override string TransitionPowerShellCommand => "Stop-VM";

    protected override void CreateCommand(PsCommandBuilder commandBuilder)
    {
        commandBuilder.AddParameter("Force");
    }
}