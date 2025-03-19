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
internal class VirtualMachineShutdownHandler(
    ITaskMessaging messaging,
    IServiceProvider serviceProvider)
    : VirtualMachineStateTransitionHandler<ShutdownVMCommand>(messaging, serviceProvider)
{
    protected override Aff<AgentRuntime, Unit> HandleCommand(
        OperationTask<ShutdownVMCommand> message,
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        timeout(
            TimeSpan.FromMinutes(5), 
            from powershell in default(AgentRuntime).Powershell
            from ct in cancelToken<AgentRuntime>()
            let command = PsCommandBuilder.Create()
                .AddCommand("Stop-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Force")
            from _2 in powershell.RunAsync(command, cancellationToken: ct).ToAff()
            select unit);
}
