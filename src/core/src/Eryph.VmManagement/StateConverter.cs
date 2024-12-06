using System;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Wmi;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class StateConverter
{
    /// <summary>
    /// Converts the given state information to a <see cref="VirtualMachineState"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="VirtualMachineState"/> enum represents the state information
    /// returned by the <c>Get-VM</c> Cmdlet. WMI returns the state information in
    /// three different values. In this method, we convert the WMI state information
    /// as good as we can. Unfortunately, the documentation is incomplete and in some
    /// places even inconsistent.
    /// </remarks>
    internal static Option<VirtualMachineState> ConvertVMState(
        Option<MsvmComputerSystemEnabledState> enabledState,
        Option<string> otherEnabledState,
        Option<MsvmComputerSystemHealthState> healthState) =>
        from validEnabledState in enabledState
        from convertedState in validEnabledState switch
        {
            MsvmComputerSystemEnabledState.Other =>
                otherEnabledState.Bind(convertOtherState),
            _ => convert(validEnabledState)
        }
        let isCritical = healthState.Map(s => s != MsvmComputerSystemHealthState.Ok)
            .IfNone(true)
        select isCritical ? toCritical(convertedState) : convertedState;

    private static Option<VirtualMachineState> convert(
        MsvmComputerSystemEnabledState state) =>
        state switch
        {
            MsvmComputerSystemEnabledState.Enabled => VirtualMachineState.Running,
            MsvmComputerSystemEnabledState.Disabled => VirtualMachineState.Off,
            MsvmComputerSystemEnabledState.ShuttingDown => VirtualMachineState.Stopping,
            MsvmComputerSystemEnabledState.Quiesce => VirtualMachineState.Paused,
            MsvmComputerSystemEnabledState.Starting => VirtualMachineState.Starting,
            MsvmComputerSystemEnabledState.Paused => VirtualMachineState.Paused,
            MsvmComputerSystemEnabledState.Suspended => VirtualMachineState.Saved,
            MsvmComputerSystemEnabledState.Starting2 => VirtualMachineState.Starting,
            MsvmComputerSystemEnabledState.Saving => VirtualMachineState.Saving,
            MsvmComputerSystemEnabledState.Stopping => VirtualMachineState.Stopping,
            MsvmComputerSystemEnabledState.Pausing => VirtualMachineState.Pausing,
            MsvmComputerSystemEnabledState.Resuming => VirtualMachineState.Resuming,
            _ => None,
        };

    private static VirtualMachineState toCritical(
        VirtualMachineState state) =>
        state switch
        {
            VirtualMachineState.Running => VirtualMachineState.RunningCritical,
            VirtualMachineState.Off => VirtualMachineState.OffCritical,
            VirtualMachineState.Stopping => VirtualMachineState.StoppingCritical,
            VirtualMachineState.Saved => VirtualMachineState.SavedCritical,
            VirtualMachineState.Paused => VirtualMachineState.PausedCritical,
            VirtualMachineState.Starting => VirtualMachineState.StartingCritical,
            VirtualMachineState.Reset => VirtualMachineState.ResetCritical,
            VirtualMachineState.Saving => VirtualMachineState.SavingCritical,
            VirtualMachineState.Pausing => VirtualMachineState.PausingCritical,
            VirtualMachineState.Resuming => VirtualMachineState.ResumingCritical,
            VirtualMachineState.FastSaved => VirtualMachineState.FastSavedCritical,
            VirtualMachineState.FastSaving => VirtualMachineState.FastSavingCritical,
            _ => state
        };

    internal static Option<VirtualMachineState> convertOtherState(
        string otherState) => 
        otherState switch
        {
            _ when string.Equals(otherState, "Quiescing", StringComparison.OrdinalIgnoreCase) => VirtualMachineState.Pausing,
            _ when string.Equals(otherState, "Resuming", StringComparison.OrdinalIgnoreCase) => VirtualMachineState.Resuming,
            _ when string.Equals(otherState, "Saving", StringComparison.OrdinalIgnoreCase) => VirtualMachineState.Saving,
            _ when string.Equals(otherState, "FastSaving", StringComparison.OrdinalIgnoreCase) => VirtualMachineState.FastSaving,
            _ => None,
        };
}
