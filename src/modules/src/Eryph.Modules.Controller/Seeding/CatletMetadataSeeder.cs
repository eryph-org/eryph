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

        var metadata = JsonSerializer.Deserialize<Resources.Machines.CatletMetadata>(json);
        if (metadata is null)
            throw new SeederException($"The catlet metadata {entityId} is invalid");

        await _metadataService.SaveMetadata(metadata, cancellationToken);
        await _metadataRepository.SaveChangesAsync(cancellationToken);
    }
}
