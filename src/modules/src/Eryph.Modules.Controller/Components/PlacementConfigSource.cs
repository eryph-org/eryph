using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Builds the <see cref="ConfigDomain.PlacementConfig"/> payload from the
/// controller-owned placement configuration.
/// </summary>
internal sealed class PlacementConfigSource(
    IPlacementConfigProvider provider)
    : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.PlacementConfig;

    public async Task<string> BuildPayloadAsync(CancellationToken cancellationToken)
    {
        var config = await provider.GetPlacementConfigAsync(cancellationToken);
        return JsonSerializer.Serialize(config);
    }
}
