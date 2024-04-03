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
using Eryph.StateDb.Specifications;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState.VirtualMachines;

internal class ZeroStateCatletMetadataSeeder : ZeroStateSeederBase
{
    private readonly ILogger _logger;
    private readonly IStateStoreRepository<CatletMetadata> _metadataRepository;

    public ZeroStateCatletMetadataSeeder(
        IFileSystem fileSystem,
        IZeroStateConfig config,
        ILogger logger,
        IStateStoreRepository<CatletMetadata> metadataRepository)
        : base(fileSystem, config.VirtualMachinesConfigPath)
    {
        _logger = logger;
        _metadataRepository = metadataRepository;
    }

    protected override async Task SeedAsync(
        Guid entityId,
        string json,
        CancellationToken cancellationToken = default)
    {
        bool exists = await _metadataRepository.AnyAsync(
            new CatletMetadataSpecs.GetById(entityId),
            cancellationToken);
        if (exists)
            return;
        
        var metadata = JsonSerializer.Deserialize<Resources.Machines.CatletMetadata>(json);
        if (metadata is null)
        {
            _logger.LogWarning("Could not deserialize catlet metadata {MetadataId}", entityId);
            return;
        }

        await _metadataRepository.AddAsync(new CatletMetadata()
        {
            Id = entityId,
            Metadata = JsonSerializer.Serialize(metadata),
        },
        cancellationToken);
        await _metadataRepository.SaveChangesAsync(cancellationToken);
    }
}
