using Haipa.Messages;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.VmManagement;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class VirtualMachineStartHandler : VirtualMachineStateTransitionHandler<StartMachineCommand>
    {

        public VirtualMachineStartHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }
        
        protected override string TransitionPowerShellCommand => "Start-VM";
    }
}