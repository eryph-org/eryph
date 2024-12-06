using System;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Wmi;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class OperationalStatusConverter
{
    public static Option<VirtualMachineOperationalStatus> Convert(
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
        from result in Convert(primaryStatus, secondaryStatus)
        select result;

    public static Option<VirtualMachineOperationalStatus> Convert(
        Option<VirtualMachineOperationalStatus> primaryStatus,
        Option<VirtualMachineOperationalStatus> secondaryStatus) =>
        // We just assume that the secondary status is more relevant
        // than the primary status. According to the documentation for
        // the WMI class Msvm_ComputerSystem, primary status and secondary
        // status have distinct values but the Hyper-V Cmdlets use the
        // same enum.
        secondaryStatus | primaryStatus;

    public static Option<VirtualMachineOperationalStatus> Convert(
        Option<MsvmComputerSystemOperationalStatus> primaryStatus,
        Option<MsvmComputerSystemOperationalStatus> secondaryStatus) =>
        Convert(Convert(secondaryStatus), Convert(primaryStatus));

    private static Option<VirtualMachineOperationalStatus> Convert(
        Option<MsvmComputerSystemOperationalStatus> status) =>
        status.Bind(Convert);

    private static Option<VirtualMachineOperationalStatus> Convert(
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
