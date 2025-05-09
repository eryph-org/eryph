﻿using System;
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

using static Eryph.Core.Prelude;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

using static VirtualMachineUtils<AgentRuntime>;

[UsedImplicitly]
internal class VirtualMachineStartHandler(
    ITaskMessaging messaging,
    Scope serviceScope)
    : IHandleMessages<OperationTask<StartCatletVMCommand>>
{
    public async Task Handle(OperationTask<StartCatletVMCommand> message)
    {
        var result = await HandleCommand(message.Command)
            .Run(AgentRuntime.New(serviceScope));

        await result.FailOrComplete(messaging, message);
    }

    private static Aff<AgentRuntime, CatletStateResponse> HandleCommand(
        StartCatletVMCommand command) =>
        from powershell in default(AgentRuntime).Powershell
        from vmInfo in getVmInfo(command.VMId)
        from _ in timeout(
            EryphConstants.OperationTimeout,
            from ct in cancelToken<AgentRuntime>()
            let stopCommand = PsCommandBuilder.Create()
                .AddCommand("Start-VM")
                .AddParameter("VM", vmInfo.PsObject)
            from _ in powershell.RunAsync(stopCommand, withoutLock: true, cancellationToken: ct).ToAff()
            select unit)
            // Starting a VM is best-effort. Hyper-V might wait for a response from
            // guest integration which can cause the command to block for a long time.
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
