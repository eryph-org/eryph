namespace Eryph.VmManagement.Wmi;

/// <summary>
/// Represents the <c>HealthState</c> included in the WMI
/// class <c>Msvm_ComputerSystem</c>.
/// </summary>
/// <remarks>
/// The documentation can be found at
/// <see href="https://learn.microsoft.com/en-us/windows/win32/hyperv_v2/msvm-computersystem"/>.
/// </remarks>
internal enum MsvmComputerSystemHealthState
{
    Ok = 5,
    MajorFailure = 20,
    CriticalFailure = 25,
}