namespace Eryph.VmManagement.Wmi;

/// <summary>
/// Represents the <c>EnabledState</c> included in the WMI
/// class <c>Msvm_ComputerSystem</c>.
/// </summary>
/// <remarks>
/// The documentation can be found at
/// <see href="https://learn.microsoft.com/en-us/windows/win32/hyperv_v2/msvm-computersystem"/>.
/// </remarks>
internal enum MsvmComputerSystemEnabledState
{
    Unknown = 0,
    Other = 1,
    Enabled = 2,
    Disabled = 3,
    ShuttingDown = 4,
    NotApplicable = 5,
    EnabledButOffline = 6,
    InTest = 7,
    Deferred = 8,
    Quiesce = 9,
    Starting = 10,

    // These values were taken from the chart of a state machine
    // included in the documentation.
    Paused = 32768,
    Suspended = 32769,
    // There are two Starting values defined in different places
    // of the documentation.
    Starting2 = 32770,
    Saving = 32773,
    Stopping = 32774,
    Pausing = 32776,
    Resuming = 32777,
}
