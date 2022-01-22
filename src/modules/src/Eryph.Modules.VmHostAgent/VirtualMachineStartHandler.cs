using Eryph.Messages.Resources.Machines.Commands;
using Eryph.VmManagement;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class VirtualMachineStartHandler : VirtualMachineStateTransitionHandler<StartVMCommand>
    {
        public VirtualMachineStartHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }

        protected override string TransitionPowerShellCommand => "Start-VM";
    }
}