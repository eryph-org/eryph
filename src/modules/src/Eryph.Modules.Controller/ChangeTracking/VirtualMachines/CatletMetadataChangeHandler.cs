using System;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller.Serializers;
using Eryph.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.ChangeTracking.VirtualMachines;

internal class CatletMetadataChangeHandler : IChangeHandler<CatletMetadataChange>
{
    private readonly ChangeTrackingConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;

    public CatletMetadataChangeHandler(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        CatletMetadataChange change,
        CancellationToken cancellationToken = default)
    {
        var metadataId = change.MetadataId;
        var path = Path.Combine(_config.VirtualMachinesConfigPath, $"{metadataId}.json");

        var metadata = await _stateStore.For<CatletMetadata>()
            .GetByIdAsync(metadataId, cancellationToken);
        if (metadata is null)
        {
            _fileSystem.File.Delete(path);
            return;
        }

        var metadataConfig = new CatletMetadataConfigModel
        {
            Id = metadata.Id,
            CatletId = metadata.CatletId,
            VmId = metadata.VmId,
            Metadata = CatletMetadataContentJsonSerializer.SerializeToElement(metadata.Metadata),
            IsDeprecated = metadata.IsDeprecated,
            SecretDataHidden = metadata.SecretDataHidden,
        };

        var json = CatletMetadataConfigModelJsonSerializer.Serialize(metadataConfig);
        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
