namespace Haipa.VmManagement.Data
{
    public enum VMComputerSystemState
    {
        Unknown = 0,
        Other = 1,
        Running = 2,
        PowerOff = 3,
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
    }
}