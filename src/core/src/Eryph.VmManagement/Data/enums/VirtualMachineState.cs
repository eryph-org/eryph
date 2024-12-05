namespace Eryph.VmManagement.Data;

/// <summary>
/// The values for <c>State</c> as returned by the
/// Powershell Cmdlet <c>Get-VM</c>.
/// </summary>
/// <remarks>
/// The numerical values of the corresponding Powershell
/// might be different depending on the Windows version.
/// Hence, the enum must be mapped by name which fortunately
/// is the default behavior of AutoMapper.
/// </remarks>
public enum VirtualMachineState
{
    Other = 1,
    Running = 2,
    Off = 3,
    Stopping = 4,
    Saved = 6,
    Paused = 9,
    Starting = 10,
    Reset = 11,
    Saving = 32773,
    Pausing = 32776,
    Resuming = 32777,
    FastSaved = 32779,
    FastSaving = 32780,
    ForceShutdown = 32781,

    // Starting from here the numerical values differ between
    // the Windows versions. Hibernated and ComponentServicing
    // are not present on at least Windows Server 2016.
    ForceReboot = 32782,
    Hibernated = 32783,
    ComponentServicing = 32784,
    RunningCritical = 32785,
    OffCritical = 32786,
    StoppingCritical = 32787,
    SavedCritical = 32788,
    PausedCritical = 32789,
    StartingCritical = 32790,
    ResetCritical = 32791,
    SavingCritical = 32792,
    PausingCritical = 32793,
    ResumingCritical = 32794,
    FastSavedCritical = 32795,
    FastSavingCritical = 32796,
}
