namespace Eryph.VmManagement.TestBase;

/// <summary>
/// Contains constants for status values of Hyper-V WMI classes.
/// These are used for testing and are intentionally defined separately.
/// </summary>
public static class MsvmConstants
{
    public static class EnabledState
    {
        public static readonly ushort Enabled = 2;
    }

    public static class HealthState
    {
        public static readonly ushort Ok = 5;
        public static readonly ushort CriticalFailure = 25;
    }

    public static class OperationalStatus
    {
        public static readonly ushort Ok = 2;
        public static readonly ushort InService = 11;
    }
}
