namespace Eryph.Resources.Machines;

public class VirtualMachineMemoryData
{
    public int Buffer { get; set; }
    public int Priority { get; set; }

    public long Maximum { get; set; }

    public long Minimum { get; set; }

    public long Startup { get; set; }

    public bool DynamicMemoryEnabled { get; set; }
}