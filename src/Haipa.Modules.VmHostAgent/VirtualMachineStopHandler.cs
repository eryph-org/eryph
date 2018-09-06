using HyperVPlus.Messages;
using HyperVPlus.VmManagement;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class VirtualMachineStopHandler : VirtualMachineStateTransitionHandler<StopVirtualMachineCommand>
    {

        public VirtualMachineStopHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }

        protected override string TransitionPowerShellCommand => "Stop-VM";
    }
}