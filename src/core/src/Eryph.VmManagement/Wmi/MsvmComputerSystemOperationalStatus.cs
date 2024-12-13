namespace Eryph.VmManagement.Wmi;

/// <summary>
/// Represents the <c>OperationalStatus</c> included in the WMI
/// class <c>Msvm_ComputerSystem</c>.
/// </summary>
/// <remarks>
/// The documentation can be found at
/// <see href="https://learn.microsoft.com/en-us/windows/win32/hyperv_v2/msvm-computersystem"/>.
/// Some values are not documented but are present in the list of values
/// which can be returned by the <c>Get-VM</c> Powershell Cmdlet.
/// We assume that Microsoft just forgot to document them.
/// </remarks>
public enum MsvmComputerSystemOperationalStatus : ushort
{
    Ok = 2,
    Degraded = 3,
    PredictiveFailure = 5,
    InService = 11,
    Dormant = 15,

    // This value is not documented but exists in the Powershell Cmdlet.
    SupportingEntityInError = 16,

    CreatingSnapshot = 32768,
    ApplyingSnapshot = 23769,
    DeletingSnapshot = 32770,
    WaitingToStart = 32771,
    MergingDisks = 32772,
    ExportingVirtualMachine = 32773,
    MigratingVirtualMachine = 32774,

    // These values are not documented but exist in the Powershell Cmdlet.
    BackingUpVirtualMachine = 32776,
    ModifyingUpVirtualMachine = 32777,
    StorageMigrationPhaseOne = 32778,
    StorageMigrationPhaseTwo = 32779,
    MigratingPlannedVm = 32780,
    CheckingCompatibility = 32781,
    ApplicationCriticalState = 32782,
    CommunicationTimedOut = 32783,
    CommunicationFailed = 32784,
    NoIommu = 32785,
    NoIovSupportInNic = 32786,
    SwitchNotInIovMode = 32787,
    IovBlockedByPolicy = 32788,
    IovNoAvailResources = 32789,
    IovGuestDriversNeeded = 32790,
    CriticalIoError = 32795,
}