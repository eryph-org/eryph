﻿using System;
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

        var document = JsonDocument.Parse(json);
        var version = CatletMetadataConfigModelJsonSerializer.GetVersion(document);
        if (version == 1)
        {
            await SeedV1Metadata(document, cancellationToken);
        }
        else if (version == 2)
        {
            await SeedMetadata(document, cancellationToken);
        }
        else
        {
            throw new SeederException($"The catlet metadata {entityId} has the unsupported version {version}.");
        }

        await _metadataRepository.SaveChangesAsync(cancellationToken);
    }


    private async Task SeedV1Metadata(
        JsonDocument document,
        CancellationToken cancellationToken)
    {
        var metadataInfo = CatletMetadataConfigModelJsonSerializer.DeserializeV1(document);
        var metadata = new CatletMetadata
        {
            Id = metadataInfo.Id,
            CatletId = metadataInfo.CatletId,
            VmId = metadataInfo.VmId,
            IsDeprecated = true,
        };
        await _metadataService.AddMetadata(metadata, cancellationToken);
    }

    private async Task SeedMetadata(
        JsonDocument document,
        CancellationToken cancellationToken)
    {
        var metadataConfig = CatletMetadataConfigModelJsonSerializer.Deserialize(document);
        var metadata = new CatletMetadata
        {
            Id = metadataConfig.Id,
            CatletId = metadataConfig.CatletId,
            VmId = metadataConfig.VmId,
            IsDeprecated = metadataConfig.IsDeprecated,
            SecretDataHidden = metadataConfig.SecretDataHidden,
            Metadata = CatletMetadataJsonSerializer.Deserialize(metadataConfig.Metadata),
        };

        await _metadataService.AddMetadata(metadata, cancellationToken);
    }
}
