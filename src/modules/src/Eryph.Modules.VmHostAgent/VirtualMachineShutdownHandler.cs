using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Handlers;

using static Eryph.Core.EryphPrelude;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class VirtualMachineShutdownHandler(
    ITaskMessaging messaging,
    IServiceProvider serviceProvider)
    : IHandleMessages<OperationTask<ShutdownVMCommand>>
{
    public async Task Handle(OperationTask<ShutdownVMCommand> message)
    {
        var result = await HandleCommand(message.Command)
            .Run(AgentRuntime.New(serviceProvider));

        await result.FailOrComplete(messaging, message);
    }

    protected static Aff<AgentRuntime, CatletStateResponse> HandleCommand(ShutdownVMCommand command) =>
        from powershell in default(AgentRuntime).Powershell
        from vmInfo in VmQueries.GetVmInfo(powershell, command.VMId).ToAff()
        from _ in timeout(
            EryphConstants.OperationTimeout,
            from ct in cancelToken<AgentRuntime>()
            let stopCommand = PsCommandBuilder.Create()
                .AddCommand("Stop-VM")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Force")
            from _ in powershell.RunAsync(stopCommand, cancellationToken: ct).ToAff()
            select unit)
            | @catchError(
                e => e is PowershellError { Category: PowershellErrorCategory.PipelineStopped },
                _ => unitAff)
        let timestamp = DateTimeOffset.UtcNow
        from reloadedVmInfo in VmQueries.GetVmInfo(powershell, command.VMId).ToAff()
        select new CatletStateResponse
        {
            Status = VmStateUtils.toVmStatus(reloadedVmInfo.Value.State),
            UpTime = reloadedVmInfo.Value.Uptime,
            Timestamp = timestamp,
        };
}
