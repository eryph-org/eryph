using System;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Reads the operator-defined placement configuration from a JSON file. The file
/// is the operator's editing surface; an empty/missing file yields an empty
/// placement configuration.
/// </summary>
internal sealed class FilePlacementConfigProvider(
    PlacementConfigOptions options,
    IFileSystem fileSystem,
    ILogger<FilePlacementConfigProvider> logger)
    : IPlacementConfigProvider
{
    public async Task<PlacementConfig> GetPlacementConfigAsync(CancellationToken cancellationToken)
    {
        var path = options.ConfigPath;
        if (string.IsNullOrWhiteSpace(path) || !fileSystem.File.Exists(path))
            return new PlacementConfig();

        try
        {
            var json = await fileSystem.File.ReadAllTextAsync(path, cancellationToken);
            return string.IsNullOrWhiteSpace(json)
                ? new PlacementConfig()
                : JsonSerializer.Deserialize<PlacementConfig>(json) ?? new PlacementConfig();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read placement configuration from {Path}; using empty.", path);
            return new PlacementConfig();
        }
    }
}
