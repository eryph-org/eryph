using System;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller.Serializers;
using Eryph.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.ChangeTracking.VirtualMachines;

internal class CatletMetadataChangeHandler(
    ChangeTrackingConfig config,
    IFileSystem fileSystem,
    IStateStore stateStore)
    : IChangeHandler<CatletMetadataChange>
{
    public async Task HandleChangeAsync(
        CatletMetadataChange change,
        CancellationToken cancellationToken = default)
    {
        var metadataId = change.MetadataId;
        var path = Path.Combine(config.VirtualMachinesConfigPath, $"{metadataId}.json");

        var metadata = await stateStore.For<CatletMetadata>()
            .GetByIdAsync(metadataId, cancellationToken);
        if (metadata is null)
        {
            fileSystem.File.Delete(path);
            return;
        }

        var metadataConfig = new CatletMetadataConfigModel
        {
            Id = metadata.Id,
            CatletId = metadata.CatletId,
            VmId = metadata.VmId,
            Metadata = CatletMetadataContentJsonSerializer.SerializeToElement(
                metadata.Metadata ?? throw new InvalidOperationException(
                    $"The metadata for catlet {metadata.CatletId} has no content.")),
            IsDeprecated = metadata.IsDeprecated,
            SecretDataHidden = metadata.SecretDataHidden,
            SpecificationId = metadata.SpecificationId,
            SpecificationVersionId = metadata.SpecificationVersionId,
        };

        var json = CatletMetadataConfigModelJsonSerializer.Serialize(metadataConfig);
        await fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
