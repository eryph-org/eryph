using Eryph.Resources.Machines.Config;

namespace Eryph.Modules.Controller
{
    public interface IPlacementCalculator
    {
        string CalculateVMPlacement(MachineConfig? dataConfig);
    }
}