using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Seeding;

internal class CatletMetadataSeeder : SeederBase
{
    private readonly IStateStoreRepository<CatletMetadata> _metadataRepository;
    private readonly IVirtualMachineMetadataService _metadataService;

    public CatletMetadataSeeder(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStoreRepository<CatletMetadata> metadataRepository,
        IVirtualMachineMetadataService metadataService)
        : base(fileSystem, config.VirtualMachinesConfigPath)
    {
        _metadataRepository = metadataRepository;
        _metadataService = metadataService;
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

        Resources.Machines.CatletMetadata metadata;
        try
        {
            metadata = CatletMetadataJsonSerializer.Deserialize(json);
        }
        catch
        {
            // If the metadata cannot be deserialized, we assume it is an old version.
            // In this case, we extract some IDs from the JSON and create
            // a minimal metadata entry.
            var metadataInfo = CatletMetadataJsonSerializer.DeserializeInfo(json);
            // TODO mark as deprecated metadata to block updates
            var dbMetadata = new CatletMetadata
            {
                Id = metadataInfo.Id,
            };
            await _metadataRepository.AddAsync(dbMetadata, cancellationToken);
            await _metadataRepository.SaveChangesAsync(cancellationToken);

            return;
        }

        await _metadataService.SaveMetadata(metadata, cancellationToken);
        await _metadataRepository.SaveChangesAsync(cancellationToken);
    }
}
