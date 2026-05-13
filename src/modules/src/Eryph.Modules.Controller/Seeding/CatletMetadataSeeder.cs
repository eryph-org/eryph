using System;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Serializers;
using Eryph.Resources.Machines;
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

        if (!root.TryGetProperty("MachineId", out var catletId))
            throw new JsonException("The catlet metadata JSON does not contain a catlet ID.");

        if (!root.TryGetProperty("VMId", out var vmId))
            throw new JsonException("The catlet metadata JSON does not contain a VM ID.");

        var metadata = new CatletMetadata
        {
            Id = id.GetGuid(),
            CatletId = catletId.GetGuid(),
            VmId = vmId.GetGuid(),
            IsDeprecated = true,
            SecretDataHidden = root.TryGetProperty("SecureDataHidden", out var secretDataHidden)
                               && secretDataHidden.GetBoolean(),
            Metadata = SalvageContent(root),
        };

        await _metadataService.AddMetadata(metadata, cancellationToken);
    }

    /// <summary>
    /// Salvage what we can from a v0.4 metadata document so the
    /// read-only endpoints can still report at least the parent of
    /// deprecated catlets. The result is intentionally incomplete:
    /// <see cref="CatletMetadataContent.PinnedGenes"/> are not recoverable
    /// because v0.4 never persisted gene hashes, so deprecated catlets
    /// remain locked for write operations.
    /// </summary>
    private static CatletMetadataContent SalvageContent(JsonElement root)
    {
        return new CatletMetadataContent
        {
            Architecture = SalvageArchitecture(root),
            Config = new CatletConfig
            {
                Parent = SalvageString(root, "Parent"),
            },
            ContentType = "",
            OriginalConfig = "",
        };
    }

    private static Architecture SalvageArchitecture(JsonElement root)
    {
        var rawArchitecture = SalvageString(root, "Architecture");
        if (string.IsNullOrEmpty(rawArchitecture))
            return Architecture.New(EryphConstants.DefaultArchitecture);

        // The v0.4 metadata may contain an architecture value which the
        // current validation rejects. Fall back to the default rather
        // than aborting the entire seeding pass for a single bad record.
        return Architecture.NewEither(rawArchitecture).IfLeft(
            _ => Architecture.New(EryphConstants.DefaultArchitecture));
    }

    private static string? SalvageString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element)
        && element.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(element.GetString())
            ? element.GetString()
            : null;

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
            SpecificationId = metadataConfig.SpecificationId,
            SpecificationVersionId = metadataConfig.SpecificationVersionId,
        };

        await _metadataService.AddMetadata(metadata, cancellationToken);
    }
}
