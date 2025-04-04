using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Handlers;
using SimpleInjector;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

using static VirtualMachineUtils<AgentRuntime>;

[UsedImplicitly]
internal class VirtualMachineStopHandler(
    ITaskMessaging messaging,
    Scope serviceScope)
    : IHandleMessages<OperationTask<StopVMCommand>>
{
    public async Task Handle(OperationTask<StopVMCommand> message)
    {
        var result = await HandleCommand(message.Command)
            .Run(AgentRuntime.New(serviceScope));

        await result.FailOrComplete(messaging, message);
    }

    private static Aff<AgentRuntime, CatletStateResponse> HandleCommand(StopVMCommand command) =>
        from powershell in default(AgentRuntime).Powershell
        from vmInfo in getVmInfo(command.VMId)
        from _ in timeout(
            EryphConstants.OperationTimeout,
            from ct in cancelToken<AgentRuntime>()
            let stopCommand = PsCommandBuilder.Create()
                .AddCommand("Stop-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("TurnOff")
            from _ in powershell.RunAsync(stopCommand, withoutLock: true, cancellationToken: ct).ToAff()
            select unit)
        let timestamp = DateTimeOffset.UtcNow
        from reloadedVmInfo in getVmInfo(command.VMId)
        select new CatletStateResponse
        {
            Status = VmStateUtils.toVmStatus(reloadedVmInfo.Value.State),
            UpTime = reloadedVmInfo.Value.Uptime,
            Timestamp = timestamp,
        };
}
