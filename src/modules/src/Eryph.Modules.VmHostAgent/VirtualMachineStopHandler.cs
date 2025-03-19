using System;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class VirtualMachineStopHandler(
    ITaskMessaging messaging,
    IServiceProvider serviceProvider)
    : VirtualMachineStateTransitionHandler<StopVMCommand>(messaging, serviceProvider)
{
    protected override Aff<AgentRuntime, Unit> HandleCommand(
        OperationTask<StopVMCommand> message,
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        timeout(
            TimeSpan.FromMinutes(5),
            from powershell in default(AgentRuntime).Powershell
            let command = PsCommandBuilder.Create()
                .AddCommand("Stop-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("TurnOff")
            from _1 in powershell.RunAsync(command).ToAff()
            select unit);
}
