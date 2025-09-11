using System;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Serializers;
using Eryph.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.Controller.Seeding;

internal class CatletMetadataSeeder : SeederBase
{
    private readonly IStateStoreRepository<CatletMetadata> _metadataRepository;
    private readonly ICatletMetadataService _metadataService;

    public CatletMetadataSeeder(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStoreRepository<CatletMetadata> metadataRepository,
        ICatletMetadataService metadataService)
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
            new CatletMetadataSpecs.GetByIdReadonly(entityId),
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
        var root = document.RootElement;

        if (!root.TryGetProperty("Id", out var id))
            throw new JsonException("The catlet metadata JSON does not contain an ID.");

        if (!root.TryGetProperty("CatletId", out var catletId) && !root.TryGetProperty("MachineId", out catletId))
            throw new JsonException("The catlet metadata JSON does not contain a catlet ID.");

        if (!root.TryGetProperty("VmId", out var vmId) && !root.TryGetProperty("VMId", out vmId))
            throw new JsonException("The catlet metadata JSON does not contain a VM ID.");

        var metadata = new CatletMetadata
        {
            Id = id.GetGuid(),
            CatletId = catletId.GetGuid(),
            VmId = vmId.GetGuid(),
            IsDeprecated = true,
            SecretDataHidden = root.TryGetProperty("SecureDataHidden", out var secretDataHidden)
                               && secretDataHidden.GetBoolean(),
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
            Metadata = metadataConfig.Metadata.HasValue
                ? CatletMetadataContentJsonSerializer.Deserialize(metadataConfig.Metadata.Value)
                : null,
        };

        await _metadataService.AddMetadata(metadata, cancellationToken);
    }
}
