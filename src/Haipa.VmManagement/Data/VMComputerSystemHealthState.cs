namespace Haipa.VmManagement.Data
{
    public enum VMComputerSystemHealthState
    {
        Unknown = 0,
        Ok = 5,
        MajorFailure = 20, // 0x00000014
        CriticalFailure = 25, // 0x00000019
    }
}