using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Supplies the controller-owned, operator-defined <see cref="PlacementConfig"/>
/// (datastore/environment name catalog) that is distributed to agents. The host
/// supplies an operator-backed implementation (file- or API-sourced); the default
/// is empty until that is wired.
/// </summary>
internal interface IPlacementConfigProvider
{
    Task<PlacementConfig> GetPlacementConfigAsync(CancellationToken cancellationToken);
}
