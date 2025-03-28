using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Sys;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;
using SimpleInjector;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

using static VirtualMachineUtils<AgentRuntime>;

[UsedImplicitly]
internal class VirtualMachineKillHandler(
    ITaskMessaging messaging,
    Scope serviceScope)
    : IHandleMessages<OperationTask<KillVMCommand>>
{
    public async Task Handle(OperationTask<KillVMCommand> message)
    {
        var result = await HandleCommand(message.Command)
            .Run(AgentRuntime.New(serviceScope));

        await result.FailOrComplete(messaging, message);
    }

    protected Aff<AgentRuntime, CatletStateResponse> HandleCommand(KillVMCommand command) =>
        // Unfortunately, the Get-VM Cmdlet does not expose the process ID
        // of the VM's worker process. Hence, we query Hyper-V via WMI.
        from optionalProcessId in WmiQueries<AgentRuntime>.getVmProcessId(command.VMId)
        from processId in optionalProcessId.ToAff(Error.New(
            $"The VM with ID {command.VMId} has no worker process."))
        from powershell in default(AgentRuntime).Powershell
        from _ in timeout(
            EryphConstants.OperationTimeout,
            from ct in cancelToken<AgentRuntime>()
            let stopProcessCommand = PsCommandBuilder.Create()
                .AddCommand("Stop-Process")
                .AddParameter("Id", processId)
                .AddParameter("Force")
            from _1 in powershell.RunAsync(stopProcessCommand, cancellationToken: ct).ToAff()
            // Server 2016 restarts the VM after the worker process has been killed.
            // We retry the power off command until it is successful. This forces the
            // VM into a valid off state.
            from _2 in retry(
                Schedule.spaced(TimeSpan.FromSeconds(5)),
                from vmInfo in getVmInfo(command.VMId)
                from _ in stopVm(vmInfo)
                select unit)
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
