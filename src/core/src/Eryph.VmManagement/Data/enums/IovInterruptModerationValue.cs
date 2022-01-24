namespace Eryph.VmManagement.Data
{
    public enum IovInterruptModerationValue
    {
        Default = 0,
        Adaptive = 1,
        Off = 2,
        Low = 100, // 0x00000064
        Medium = 200, // 0x000000C8
        High = 300 // 0x0000012C
    }
}