namespace Haipa.VmManagement.Data
{
    public enum VirtualMachineState
    {
        Other = 1,
        Running = 2,
        Off = 3,
        Stopping = 4,
        Saved = 6,
        Paused = 9,
        Starting = 10, // 0x0000000A
        Reset = 11, // 0x0000000B
        Saving = 32773, // 0x00008005
        Pausing = 32776, // 0x00008008
        Resuming = 32777, // 0x00008009
        FastSaved = 32779, // 0x0000800B
        FastSaving = 32780, // 0x0000800C
        ForceShutdown = 32781, // 0x0000800D
        ForceReboot = 32782, // 0x0000800E
        RunningCritical = 32783, // 0x0000800F
        OffCritical = 32784, // 0x00008010
        StoppingCritical = 32785, // 0x00008011
        SavedCritical = 32786, // 0x00008012
        PausedCritical = 32787, // 0x00008013
        StartingCritical = 32788, // 0x00008014
        ResetCritical = 32789, // 0x00008015
        SavingCritical = 32790, // 0x00008016
        PausingCritical = 32791, // 0x00008017
        ResumingCritical = 32792, // 0x00008018
        FastSavedCritical = 32793, // 0x00008019
        FastSavingCritical = 32794, // 0x0000801A
    }
}