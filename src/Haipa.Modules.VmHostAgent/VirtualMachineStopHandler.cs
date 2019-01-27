using Haipa.Messages;
using Haipa.VmManagement;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class VirtualMachineStopHandler : VirtualMachineStateTransitionHandler<StopMachineCommand>
    {

        public VirtualMachineStopHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }

        protected override string TransitionPowerShellCommand => "Stop-VM";
    }
}