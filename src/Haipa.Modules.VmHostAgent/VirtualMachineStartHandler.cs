using HyperVPlus.Messages;
using HyperVPlus.VmManagement;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class VirtualMachineStartHandler : VirtualMachineStateTransitionHandler<StartVirtualMachineCommand>
    {

        public VirtualMachineStartHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }
        
        protected override string TransitionPowerShellCommand => "Start-VM";
    }
}