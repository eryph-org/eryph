using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines.Config;

namespace Haipa.Modules.Controller
{
    public interface IPlacementCalculator
    {
        string CalculateVMPlacement(MachineConfig dataConfig);
    }
}