using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.Controller
{
    public interface IPlacementCalculator
    {
        string CalculateVMPlacement(CatletConfig? dataConfig);
    }
}