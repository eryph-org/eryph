using Haipa.VmConfig;

namespace Haipa.Modules.Controller
{
    public interface IPlacementCalculator
    {
        string CalculateVMPlacement(MachineConfig dataConfig);
    }
}