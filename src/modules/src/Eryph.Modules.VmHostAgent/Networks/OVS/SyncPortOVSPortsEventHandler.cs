using System;
using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.VmManagement;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using SimpleInjector;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

using static OvsPortCommands<AgentRuntime>;
using static Logger<AgentRuntime>;

[UsedImplicitly]
internal class SyncPortOVSPortsEventHandler(
    ILogger logger,
    Scope serviceScope)
    : IHandleMessages<VirtualMachineStateChangedEvent>
{
    public async Task Handle(VirtualMachineStateChangedEvent message)
    {
        var result = await HandleEvent(message).Run(AgentRuntime.New(serviceScope));
        result.IfFail(e => logger.LogError(e, "Failed to sync OVS network ports"));
    }

    private static Aff<AgentRuntime, Unit> HandleEvent(VirtualMachineStateChangedEvent @event) =>
        from portChange in @event.State switch
        {
            VirtualMachineState.Other => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.Running => SuccessEff(VMPortChange.Add),
            VirtualMachineState.Off => SuccessEff(VMPortChange.Remove),
            VirtualMachineState.Stopping => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.Saved => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.Paused => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.Starting => SuccessEff(VMPortChange.Add),
            VirtualMachineState.Reset => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.Saving => SuccessEff(VMPortChange.Remove),
            VirtualMachineState.Pausing => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.Resuming => SuccessEff(VMPortChange.Add),
            VirtualMachineState.FastSaved => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.FastSaving => SuccessEff(VMPortChange.Remove),
            VirtualMachineState.ForceShutdown => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.ForceReboot => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.RunningCritical => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.OffCritical => SuccessEff(VMPortChange.Remove),
            VirtualMachineState.StoppingCritical => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.SavedCritical => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.PausedCritical => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.StartingCritical => SuccessEff(VMPortChange.Add),
            VirtualMachineState.ResetCritical => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.SavingCritical => SuccessEff(VMPortChange.Remove),
            VirtualMachineState.PausingCritical => SuccessEff(VMPortChange.Remove),
            VirtualMachineState.ResumingCritical => SuccessEff(VMPortChange.Add),
            VirtualMachineState.FastSavedCritical => SuccessEff(VMPortChange.Nothing),
            VirtualMachineState.FastSavingCritical => SuccessEff(VMPortChange.Remove),
            _ => FailEff<VMPortChange>(Error.New($"The virtual machine state {@event.State} is not supported")),
        }
        from _ in syncOvsPorts(@event.VmId, portChange)
            .MapFail(e => Error.New($"Failed to sync the network ports of the VM {@event.VmId} after it changed to state {@event.State}", e))
        select unit;
}
