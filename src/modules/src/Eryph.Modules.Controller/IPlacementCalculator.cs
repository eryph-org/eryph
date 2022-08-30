using Eryph.ConfigModel.Machine;

namespace Eryph.Modules.Controller
{
    public interface IPlacementCalculator
    {
        string CalculateVMPlacement(MachineConfig? dataConfig);
    }
}