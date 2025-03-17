using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Sys;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class VirtualMachineStopHandler(
    ITaskMessaging messaging,
    IServiceProvider serviceProvider)
    :IHandleMessages<OperationTask<StopVMCommand>>
{
    public async Task Handle(OperationTask<StopVMCommand> message)
    {
        var result = await VirtualMachineStopHandler<AgentRuntime>
            .HandleCommand(message.Command)
            .Run(AgentRuntime.New(serviceProvider));

        await result.FailOrComplete(messaging, message);
    }
}

internal static class VirtualMachineStopHandler<RT>
    where RT : struct, HasCancel<RT>, HasPowershell<RT>, HasWmi<RT>
{
    public static Aff<RT, Unit> HandleCommand(StopVMCommand command) =>
        from powershell in default(RT).Powershell
        let getVmCommand = PsCommandBuilder.Create()
            .AddCommand("Get-VM")
            .AddParameter("Id", command.VMId)
        from vmInfos in powershell.GetObjectsAsync<VirtualMachineInfo>(getVmCommand).ToAff()
        from vmInfo in vmInfos.HeadOrNone()
            .ToAff(Error.New($"The VM with ID {command.VMId} was not found."))
        from _ in command.StopProcess
        // Improve error handling
            ? StopVm(vmInfo) | StopVmProcess(command.VMId)
            : StopVm(vmInfo)
        select unit;

    private static Aff<RT, Unit> StopVm(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from powershell in default(RT).Powershell
        let stopVmCommand = PsCommandBuilder.Create()
            .AddCommand("Stop-VM")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("TurnOff")
        // TODO add a retry
        from _ in timeout(
            TimeSpan.FromSeconds(15),
            from ct in cancelToken<RT>()
            from _ in powershell.RunAsync(stopVmCommand).ToAff()
            select unit)
        select unit;

    private static Aff<RT, Unit> StopVmProcess(Guid vmId) =>
        // Unfortunately, the Get-VM Cmdlet does not expose the process ID
        // of the VM's worker process. Hence, we query Hyper-V via WMI.
        from processId in WmiQueries<RT>.getVmProcessId(vmId)
        from _ in processId
            .Map(StopProcess)
            .SequenceSerial()
        select unit;

    private static Aff<RT, Unit> StopProcess(uint processId) =>
        from powershell in default(RT).Powershell
        let stopProcessCommand = PsCommandBuilder.Create()
            .AddCommand("Stop-Process")
            .AddParameter("Id", processId)
            .AddParameter("Force")
        from _ in powershell.RunAsync(stopProcessCommand).ToAff()
        select unit;
}
