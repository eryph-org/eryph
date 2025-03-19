using System;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Sys;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class VirtualMachineKillHandler(
    ITaskMessaging messaging,
    IServiceProvider serviceProvider)
    : VirtualMachineStateTransitionHandler<KillVMCommand>(messaging, serviceProvider)
{
    protected override Aff<AgentRuntime, Unit> HandleCommand(
        OperationTask<KillVMCommand> message,
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        // Unfortunately, the Get-VM Cmdlet does not expose the process ID
        // of the VM's worker process. Hence, we query Hyper-V via WMI.
        // TODO handle timeout
        from optionalProcessId in WmiQueries<AgentRuntime>.getVmProcessId(vmInfo.Value.Id)
        from processId in optionalProcessId.ToAff(Error.New(
            $"The VM with ID {vmInfo.Value.Id} was not found."))
        from powershell in default(AgentRuntime).Powershell
        let stopProcessCommand = PsCommandBuilder.Create()
            .AddCommand("Stop-Process")
            .AddParameter("Id", processId)
            .AddParameter("Force")
        from _1 in powershell.RunAsync(stopProcessCommand).ToAff()
        let getCommand = PsCommandBuilder.Create()
            .AddCommand("Get-VM")
            .AddParameter("Id", vmInfo.Value.Id)
        /*
        // TODO This does not work the VM will still be in status running (is there a way to force a refresh?)
        from optionalVmInfo in powershell.GetObjectAsync<VirtualMachineInfo>(getCommand).ToAff()
        from reloadedVmInfo in optionalVmInfo.ToAff(Error.New(
            $"The VM with ID {vmInfo.Value.Id} was not found."))
        let stopCommand = PsCommandBuilder.Create()
            .AddCommand("Stop-VM")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("TurnOff")
        from _2 in powershell.RunAsync(stopCommand).ToAff()
        */
        select unit;
}
