using System;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Wmi;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Inventory;

public static class VmStateUtils
{
    /// <summary>
    /// Checks if a virtual machine can be inventoried based on the
    /// provided state information.
    /// </summary>
    /// <remarks>
    /// We use this method to avoid inventorying VMs which are still
    /// being changed by Hyper-V or are in an error state where Hyper-V
    /// will not report useful information.
    /// </remarks>
    public static bool isInventorizable(
        Option<VirtualMachineState> state,
        Option<VirtualMachineOperationalStatus> operationalStatus) =>
        operationalStatus
            .Map(s => s is VirtualMachineOperationalStatus.Ok
                or VirtualMachineOperationalStatus.PredictiveFailure)
            .IfNone(false)
        && state
            .Map(s => s is VirtualMachineState.Running
                or VirtualMachineState.Off
                or VirtualMachineState.Stopping
                or VirtualMachineState.Saved
                or VirtualMachineState.Paused
                or VirtualMachineState.Starting
                or VirtualMachineState.Reset
                or VirtualMachineState.Saving
                or VirtualMachineState.Pausing
                or VirtualMachineState.Resuming
                or VirtualMachineState.FastSaved
                or VirtualMachineState.FastSaving
                or VirtualMachineState.ForceShutdown
                or VirtualMachineState.ForceReboot
                or VirtualMachineState.Hibernated)
            .IfNone(false);

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
    internal static Option<VirtualMachineState> convertMsvmState(
        Option<MsvmComputerSystemEnabledState> enabledState,
        Option<string> otherEnabledState,
        Option<MsvmComputerSystemHealthState> healthState) =>
        from validEnabledState in enabledState
        from convertedState in validEnabledState switch
        {
            MsvmComputerSystemEnabledState.Other =>
                otherEnabledState.Bind(convertOtherState),
            _ => convertMsvmEnabledState(validEnabledState)
        }
        let isCritical = healthState.Map(s => s != MsvmComputerSystemHealthState.Ok)
            .IfNone(true)
        select isCritical ? toCritical(convertedState) : convertedState;

    private static Option<VirtualMachineState> convertMsvmEnabledState(
        MsvmComputerSystemEnabledState state) =>
        state switch
        {
            MsvmComputerSystemEnabledState.Enabled => VirtualMachineState.Running,
            MsvmComputerSystemEnabledState.Disabled => VirtualMachineState.Off,
            MsvmComputerSystemEnabledState.ShuttingDown => VirtualMachineState.Stopping,
            MsvmComputerSystemEnabledState.EnabledButOffline => VirtualMachineState.Saved,
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

    private static Option<VirtualMachineState> convertOtherState(
        string otherState) =>
        otherState switch
        {
            _ when string.Equals(otherState, "Quiescing", StringComparison.OrdinalIgnoreCase) => VirtualMachineState.Pausing,
            _ when string.Equals(otherState, "Resuming", StringComparison.OrdinalIgnoreCase) => VirtualMachineState.Resuming,
            _ when string.Equals(otherState, "Saving", StringComparison.OrdinalIgnoreCase) => VirtualMachineState.Saving,
            _ when string.Equals(otherState, "FastSaving", StringComparison.OrdinalIgnoreCase) => VirtualMachineState.FastSaving,
            _ => None,
        };

    public static Option<VirtualMachineOperationalStatus> convertMsvmOperationalStatus(
        VirtualMachineOperationalStatus[] operationalStatus) =>
        from _ in Some(unit)
            // Explicitly check that the enum values are defined. This is a
            // precaution as the values might be extracted from the Powershell
            // using AutoMapper.
        let primaryStatus = operationalStatus.Length >= 1
            ? Optional(operationalStatus[0]).Filter(Enum.IsDefined)
            : None
        let secondaryStatus = operationalStatus.Length >= 2
            ? Optional(operationalStatus[1]).Filter(Enum.IsDefined)
            : None
        from result in convertMsvmOperationalStatus(primaryStatus, secondaryStatus)
        select result;

    public static Option<VirtualMachineOperationalStatus> convertMsvmOperationalStatus(
        Option<MsvmComputerSystemOperationalStatus> primaryStatus,
        Option<MsvmComputerSystemOperationalStatus> secondaryStatus) =>
        convertMsvmOperationalStatus(
            convertMsvmOperationalStatus(secondaryStatus),
            convertMsvmOperationalStatus(primaryStatus));

    private static Option<VirtualMachineOperationalStatus> convertMsvmOperationalStatus(
        Option<VirtualMachineOperationalStatus> primaryStatus,
        Option<VirtualMachineOperationalStatus> secondaryStatus) =>
        // We just assume that the secondary status is more relevant
        // than the primary status. According to the documentation for
        // the WMI class Msvm_ComputerSystem, primary status and secondary
        // status have distinct values but the Hyper-V Cmdlets use the
        // same enum.
        secondaryStatus | primaryStatus;

    private static Option<VirtualMachineOperationalStatus> convertMsvmOperationalStatus(
        Option<MsvmComputerSystemOperationalStatus> status) =>
        status.Bind(convertMsvmOperationalStatus);

    private static Option<VirtualMachineOperationalStatus> convertMsvmOperationalStatus(
        MsvmComputerSystemOperationalStatus status) =>
        status switch
        {
            MsvmComputerSystemOperationalStatus.Ok => VirtualMachineOperationalStatus.Ok,
            MsvmComputerSystemOperationalStatus.Degraded => VirtualMachineOperationalStatus.Degraded,
            MsvmComputerSystemOperationalStatus.PredictiveFailure => VirtualMachineOperationalStatus.PredictiveFailure,
            MsvmComputerSystemOperationalStatus.InService => VirtualMachineOperationalStatus.InService,
            MsvmComputerSystemOperationalStatus.Dormant => VirtualMachineOperationalStatus.Dormant,
            MsvmComputerSystemOperationalStatus.SupportingEntityInError => VirtualMachineOperationalStatus.SupportingEntityInError,
            MsvmComputerSystemOperationalStatus.CreatingSnapshot => VirtualMachineOperationalStatus.CreatingSnapshot,
            MsvmComputerSystemOperationalStatus.ApplyingSnapshot => VirtualMachineOperationalStatus.ApplyingSnapshot,
            MsvmComputerSystemOperationalStatus.DeletingSnapshot => VirtualMachineOperationalStatus.DeletingSnapshot,
            MsvmComputerSystemOperationalStatus.WaitingToStart => VirtualMachineOperationalStatus.WaitingToStart,
            MsvmComputerSystemOperationalStatus.MergingDisks => VirtualMachineOperationalStatus.MergingDisks,
            MsvmComputerSystemOperationalStatus.ExportingVirtualMachine => VirtualMachineOperationalStatus.ExportingVirtualMachine,
            MsvmComputerSystemOperationalStatus.MigratingVirtualMachine => VirtualMachineOperationalStatus.MigratingVirtualMachine,
            MsvmComputerSystemOperationalStatus.BackingUpVirtualMachine => VirtualMachineOperationalStatus.BackingUpVirtualMachine,
            MsvmComputerSystemOperationalStatus.ModifyingUpVirtualMachine => VirtualMachineOperationalStatus.ModifyingUpVirtualMachine,
            MsvmComputerSystemOperationalStatus.StorageMigrationPhaseOne => VirtualMachineOperationalStatus.StorageMigrationPhaseOne,
            MsvmComputerSystemOperationalStatus.StorageMigrationPhaseTwo => VirtualMachineOperationalStatus.StorageMigrationPhaseTwo,
            MsvmComputerSystemOperationalStatus.MigratingPlannedVm => VirtualMachineOperationalStatus.MigratingPlannedVm,
            MsvmComputerSystemOperationalStatus.CheckingCompatibility => VirtualMachineOperationalStatus.CheckingCompatibility,
            MsvmComputerSystemOperationalStatus.ApplicationCriticalState => VirtualMachineOperationalStatus.ApplicationCriticalState,
            MsvmComputerSystemOperationalStatus.CommunicationTimedOut => VirtualMachineOperationalStatus.CommunicationTimedOut,
            MsvmComputerSystemOperationalStatus.CommunicationFailed => VirtualMachineOperationalStatus.CommunicationFailed,
            MsvmComputerSystemOperationalStatus.NoIommu => VirtualMachineOperationalStatus.NoIommu,
            MsvmComputerSystemOperationalStatus.NoIovSupportInNic => VirtualMachineOperationalStatus.NoIovSupportInNic,
            MsvmComputerSystemOperationalStatus.SwitchNotInIovMode => VirtualMachineOperationalStatus.SwitchNotInIovMode,
            MsvmComputerSystemOperationalStatus.IovBlockedByPolicy => VirtualMachineOperationalStatus.IovBlockedByPolicy,
            MsvmComputerSystemOperationalStatus.IovNoAvailResources => VirtualMachineOperationalStatus.IovNoAvailResources,
            MsvmComputerSystemOperationalStatus.IovGuestDriversNeeded => VirtualMachineOperationalStatus.IovGuestDriversNeeded,
            MsvmComputerSystemOperationalStatus.CriticalIoError => VirtualMachineOperationalStatus.CriticalIoError,
            _ => None
        };
}
