using Haipa.Resources.Machines.Config;

namespace Haipa.Modules.Controller
{
    public interface IPlacementCalculator
    {
        string CalculateVMPlacement(MachineConfig dataConfig);
    }
}