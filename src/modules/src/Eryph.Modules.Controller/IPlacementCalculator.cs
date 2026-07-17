using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller;

public interface IPlacementCalculator
{
    /// <summary>
    /// Selects the host agent that should run the catlet, or an error when no host in
    /// <paramref name="siteId"/> can run the requested <paramref name="architecture"/>. The
    /// implementation is provided by the runtime host.
    /// </summary>
    /// <remarks>
    /// The site is decided by the catlet's environment and resolved by the caller, which also pins it
    /// on the catlet — placement chooses the host within that site, never the site itself. A catlet
    /// cannot be placed outside the site of its environment: its storage and networks are there.
    /// </remarks>
    Either<Error, string> CalculateVMPlacement(
        CatletConfig? dataConfig,
        Guid siteId,
        Architecture architecture);
}
