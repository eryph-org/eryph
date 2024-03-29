﻿using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using JetBrains.Annotations;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class VirtualMachineStopHandler : VirtualMachineStateTransitionHandler<StopVMCommand>
    {
        public VirtualMachineStopHandler(ITaskMessaging messaging, IPowershellEngine engine) : base(messaging, engine)
        {
        }

        protected override string TransitionPowerShellCommand => "Stop-VM";

        protected override void CreateCommand(PsCommandBuilder commandBuilder)
        {
            commandBuilder.AddParameter("TurnOff");
        }
    }
}