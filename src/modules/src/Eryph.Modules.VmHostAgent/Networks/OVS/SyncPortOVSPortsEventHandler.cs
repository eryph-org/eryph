using System;
using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.VmManagement.Data;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

[UsedImplicitly]
internal class SyncPortOVSPortsEventHandler : IHandleMessages<VirtualMachineStateChangedEvent>
{
    private readonly IOVSPortManager _ovsPortManager;
    private readonly ILogger _log;

    public SyncPortOVSPortsEventHandler(IOVSPortManager ovsPortManager, ILogger log)
    {
        _ovsPortManager = ovsPortManager;
        _log = log;
    }

    public async Task Handle(VirtualMachineStateChangedEvent message)
    {
        var change = message.State switch
        {
            VirtualMachineState.Other => VMPortChange.Nothing,
            VirtualMachineState.Running => VMPortChange.Nothing,
            VirtualMachineState.Off => VMPortChange.Remove,
            VirtualMachineState.Stopping => VMPortChange.Nothing,
            VirtualMachineState.Saved => VMPortChange.Nothing,
            VirtualMachineState.Paused => VMPortChange.Nothing,
            VirtualMachineState.Starting => VMPortChange.Add,
            VirtualMachineState.Reset => VMPortChange.Nothing,
            VirtualMachineState.Saving => VMPortChange.Remove,
            VirtualMachineState.Pausing => VMPortChange.Nothing,
            VirtualMachineState.Resuming => VMPortChange.Add,
            VirtualMachineState.FastSaved => VMPortChange.Nothing,
            VirtualMachineState.FastSaving => VMPortChange.Remove,
            VirtualMachineState.ForceShutdown => VMPortChange.Nothing,
            VirtualMachineState.ForceReboot => VMPortChange.Nothing,
            VirtualMachineState.RunningCritical => VMPortChange.Nothing,
            VirtualMachineState.OffCritical => VMPortChange.Remove,
            VirtualMachineState.StoppingCritical => VMPortChange.Nothing,
            VirtualMachineState.SavedCritical => VMPortChange.Nothing,
            VirtualMachineState.PausedCritical => VMPortChange.Nothing,
            VirtualMachineState.StartingCritical => VMPortChange.Add,
            VirtualMachineState.ResetCritical => VMPortChange.Nothing,
            VirtualMachineState.SavingCritical => VMPortChange.Remove,
            VirtualMachineState.PausingCritical => VMPortChange.Remove,
            VirtualMachineState.ResumingCritical => VMPortChange.Add,
            VirtualMachineState.FastSavedCritical => VMPortChange.Nothing,
            VirtualMachineState.FastSavingCritical => VMPortChange.Remove,
            _ => throw new ArgumentOutOfRangeException()
        };

        await _ovsPortManager.SyncPorts(message.VmId, change)
            .IfLeft(l => { _log.LogError(l.Message); }).ConfigureAwait(false);
    }

}