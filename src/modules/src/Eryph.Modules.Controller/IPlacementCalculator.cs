using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller;

public interface IPlacementCalculator
{
    /// <summary>
    /// Selects the host agent that should run the catlet, or an error when the
    /// requested <paramref name="architecture"/> cannot be placed on any
    /// available host. The implementation is provided by the runtime host.
    /// </summary>
    Either<Error, string> CalculateVMPlacement(CatletConfig? dataConfig, Architecture architecture);
}
