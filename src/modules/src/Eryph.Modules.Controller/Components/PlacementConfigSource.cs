using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Settings;
using Eryph.Messages.Components;
using LanguageExt;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Builds the <see cref="ConfigDomain.PlacementConfig"/> payload from the
/// Placement section of the controller settings.
/// </summary>
internal sealed class PlacementConfigSource(
    IControllerSettingsManager settingsManager)
    : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.PlacementConfig;

    public Task<string> BuildPayloadAsync(CancellationToken cancellationToken) =>
        settingsManager.GetCurrentConfiguration()
            .Match(
                Right: settings => JsonSerializer.Serialize(settings.Placement),
                Left: _ => JsonSerializer.Serialize(new PlacementConfig()));
}
