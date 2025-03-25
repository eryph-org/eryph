using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Sys;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static Eryph.Core.Prelude;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class VirtualMachineKillHandler(
    ITaskMessaging messaging,
    IServiceProvider serviceProvider)
    : IHandleMessages<OperationTask<KillVMCommand>>
{
    public async Task Handle(OperationTask<KillVMCommand> message)
    {
        var result = await HandleCommand(message.Command)
            .Run(AgentRuntime.New(serviceProvider));

        await result.FailOrComplete(messaging, message);
    }

    protected Aff<AgentRuntime, CatletStateResponse> HandleCommand(KillVMCommand command) =>
        // Unfortunately, the Get-VM Cmdlet does not expose the process ID
        // of the VM's worker process. Hence, we query Hyper-V via WMI.
        from optionalProcessId in WmiQueries<AgentRuntime>.getVmProcessId(command.VMId)
        from processId in optionalProcessId.ToAff(Error.New(
            $"The VM with ID {command.VMId} was not found."))
        from powershell in default(AgentRuntime).Powershell
        let stopProcessCommand = PsCommandBuilder.Create()
            .AddCommand("Stop-Process")
            .AddParameter("Id", processId)
            .AddParameter("Force")
        from _ in timeout(
            EryphConstants.OperationTimeout,
            from ct in cancelToken<AgentRuntime>()
            let stopProcessCommand = PsCommandBuilder.Create()
                .AddCommand("Stop-Process")
                .AddParameter("Id", processId)
                .AddParameter("Force")
            from _1 in powershell.RunAsync(stopProcessCommand, cancellationToken: ct).ToAff()
            from _2 in repeatWhile(
                Schedule.NoDelayOnFirst & Schedule.spaced(TimeSpan.FromSeconds(5)),
                from ct in cancelToken<AgentRuntime>()
                from vmInfo in VmQueries.GetVmInfo(powershell, command.VMId).ToAff()
                select vmInfo,
                vmInfo => vmInfo.Value.State != VirtualMachineState.Off)
            select unit)
            | @catchError(
                e => e is PowershellError {Category: PowershellErrorCategory.PipelineStopped},
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
