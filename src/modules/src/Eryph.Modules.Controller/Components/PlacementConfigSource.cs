using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Settings;
using Eryph.Messages.Components;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Builds the <see cref="ConfigDomain.PlacementConfig"/> payload from the
/// Placement section of the controller settings.
/// </summary>
internal sealed class PlacementConfigSource(
    IControllerSettingsManager settingsManager,
    ILogger<PlacementConfigSource> logger)
    : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.PlacementConfig;

    public Task<string> BuildPayloadAsync(CancellationToken cancellationToken) =>
        settingsManager.GetCurrentConfiguration()
            .Match(
                Right: settings => JsonSerializer.Serialize(settings.Placement),
                Left: error =>
                {
                    // Don't distribute a silently-empty placement vocabulary: log the
                    // read failure so an operator can see why agents got nothing, and
                    // fall back to an empty config (a later refresh recovers once the
                    // settings file is readable again).
                    logger.LogError(
                        "Failed to read controller settings for {Domain}: {Error}. Distributing empty placement config.",
                        ConfigDomain.PlacementConfig, error.Message);
                    return JsonSerializer.Serialize(new PlacementConfig());
                });
}
