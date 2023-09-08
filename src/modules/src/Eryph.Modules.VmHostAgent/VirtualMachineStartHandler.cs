using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class VirtualMachineStartHandler : VirtualMachineStateTransitionHandler<StartCatletVMCommand>
    {
        public VirtualMachineStartHandler(ITaskMessaging messaging, IPowershellEngine engine) : base(messaging, engine)
        {
        }

        protected override string TransitionPowerShellCommand => "Start-VM";
    }
}