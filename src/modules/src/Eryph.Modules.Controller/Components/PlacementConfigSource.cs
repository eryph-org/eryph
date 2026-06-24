using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
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
                settings => JsonSerializer.Serialize(settings.Placement),
                error =>
                {
                    // Never distribute a silently-empty placement vocabulary — that would make
                    // agents reject every non-default datastore/environment. Fail the round
                    // instead (mirrors NetworkProvidersConfigSource); agents keep their current
                    // copy until the controller settings are readable again.
                    logger.LogError(
                        "Failed to read controller settings for {Domain}: {Error}.",
                        ConfigDomain.PlacementConfig, error.Message);
                    throw new InvalidOperationException(
                        $"Cannot distribute placement configuration: {error.Message}");
                });
}
