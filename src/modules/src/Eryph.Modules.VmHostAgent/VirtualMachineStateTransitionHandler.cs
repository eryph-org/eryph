using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal abstract class VirtualMachineStateTransitionHandler<T>(
    ITaskMessaging messaging,
    IServiceProvider serviceProvider)
    : IHandleMessages<OperationTask<T>>
    where T : class, IVMCommand, new()
{
    public async Task Handle(OperationTask<T> message)
    {
        var result = await HandleMessage(message)
            .Run(AgentRuntime.New(serviceProvider));

        await result.FailOrComplete(messaging, message);
    }

    private Aff<AgentRuntime, Unit> HandleMessage(
        OperationTask<T> message) =>
        from _ in unitAff
        from powershell in default(AgentRuntime).Powershell
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VM")
            .AddParameter("Id", message.Command.VMId)
        from optionalVmInfo in powershell.GetObjectAsync<VirtualMachineInfo>(command).ToAff()
        from vmInfo in optionalVmInfo.ToAff(Error.New($"The VM with ID {message.Command.VMId} was not found."))
        from _2 in HandleCommand(message, vmInfo)
        select unit;

    protected abstract Aff<AgentRuntime, Unit> HandleCommand(
        OperationTask<T> message,
        TypedPsObject<VirtualMachineInfo> vmInfo);
}
