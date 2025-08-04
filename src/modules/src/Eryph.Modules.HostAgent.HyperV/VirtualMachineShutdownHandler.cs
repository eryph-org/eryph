using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.HostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Handlers;
using SimpleInjector;

using static Eryph.Core.Prelude;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent;

using static VirtualMachineUtils<AgentRuntime>;

[UsedImplicitly]
internal class VirtualMachineShutdownHandler(
    ITaskMessaging messaging,
    Scope serviceScope)
    : IHandleMessages<OperationTask<ShutdownVMCommand>>
{
    public async Task Handle(OperationTask<ShutdownVMCommand> message)
    {
        var result = await HandleCommand(message.Command)
            .Run(AgentRuntime.New(serviceScope));

        await result.FailOrComplete(messaging, message);
    }

    protected static Aff<AgentRuntime, CatletStateResponse> HandleCommand(ShutdownVMCommand command) =>
        from powershell in default(AgentRuntime).Powershell
        from vmInfo in getVmInfo(command.VMId)
        from _ in timeout(
            EryphConstants.OperationTimeout,
            from ct in cancelToken<AgentRuntime>()
            let stopCommand = PsCommandBuilder.Create()
                .AddCommand("Stop-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Force")
            from _ in powershell.RunAsync(stopCommand, withoutLock: true, cancellationToken: ct).ToAff()
            select unit)
            // Shutting down a VM is best-effort. Hyper-V waits for a graceful shutdown
            // by the guest integration which can cause the command to block for a long time.
            | @catchError(
                e => e is PowershellError { Category: PowershellErrorCategory.PipelineStopped },
                _ => unitAff)
        let timestamp = DateTimeOffset.UtcNow
        from reloadedVmInfo in getVmInfo(command.VMId)
        select new CatletStateResponse
        {
            Status = VmStateUtils.toVmStatus(reloadedVmInfo.Value.State),
            UpTime = reloadedVmInfo.Value.Uptime,
            Timestamp = timestamp,
        };
}
