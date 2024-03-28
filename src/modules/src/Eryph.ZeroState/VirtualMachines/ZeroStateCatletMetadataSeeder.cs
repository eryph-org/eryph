using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState.VirtualMachines;

internal class ZeroStateCatletMetadataSeeder : ZeroStateSeederBase
{
    private readonly ILogger _logger;
    private readonly IStateStore _stateStore;

    public ZeroStateCatletMetadataSeeder(
        IFileSystem fileSystem,
        IZeroStateConfig config,
        ILogger logger,
        IStateStore stateStore)
        : base(fileSystem, config.VirtualMachinesConfigPath)
    {
        _logger = logger;
        _stateStore = stateStore;
    }

    protected override async Task SeedAsync(
        Guid entityId,
        string json,
        CancellationToken cancellationToken = default)
    {
        var existingMetadata = await _stateStore.For<CatletMetadata>()
            .GetByIdAsync(entityId, cancellationToken);
        if (existingMetadata is not null)
            return;

        var metadata = JsonSerializer.Deserialize<CatletMetadata>(json);
        if (metadata is null)
        {
            _logger.LogWarning("Could not deserialize catlet metadata {MetadataId}", entityId);
            return;
        }

        await _stateStore.For<CatletMetadata>().AddAsync(metadata, cancellationToken);
        await _stateStore.SaveChangesAsync(cancellationToken);
    }
}
