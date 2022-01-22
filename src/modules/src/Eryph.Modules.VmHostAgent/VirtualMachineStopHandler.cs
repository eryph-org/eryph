using Eryph.Messages.Resources.Machines.Commands;
using Eryph.VmManagement;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class VirtualMachineStopHandler : VirtualMachineStateTransitionHandler<StopVMCommand>
    {
        public VirtualMachineStopHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }

        protected override string TransitionPowerShellCommand => "Stop-VM";
    }
}