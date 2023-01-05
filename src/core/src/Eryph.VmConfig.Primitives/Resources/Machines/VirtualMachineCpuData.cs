namespace Eryph.Resources.Machines;

public class VirtualMachineCpuData
{
    public int Count { get; set; }

    public int Maximum { get; set; }
    public int Reserve { get; set; }
    public int RelativeWeight { get; set; }

    public int HwThreadCountPerCore { get; set; }
    public int AllowACountMCount { get; set; }
    public int MaximumCountPerNumaSocket { get; set; }

    public int MaximumCountPerNumaNode { get; set; }

    public bool ExposeVirtualizationExtensions { get; set; }
    public bool CompatibilityForMigrationEnabled { get; set; }
    public bool CompatibilityForOlderOperatingSystemsEnabled { get; set; }
    public bool EnableHostResourceProtection { get; set; }
}