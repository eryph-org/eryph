using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Default empty placement configuration, used until the host wires an
/// operator-backed provider (file- or API-sourced).
/// </summary>
internal sealed class DefaultPlacementConfigProvider : IPlacementConfigProvider
{
    public Task<PlacementConfig> GetPlacementConfigAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new PlacementConfig());
}
