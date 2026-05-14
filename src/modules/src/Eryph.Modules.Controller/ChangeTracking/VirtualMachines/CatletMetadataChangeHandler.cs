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

internal class CatletMetadataChangeHandler : IChangeHandler<CatletMetadataChange>
{
    private readonly ChangeTrackingConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;
    private readonly Microsoft.Extensions.Logging.ILogger<CatletMetadataChangeHandler> _logger;

    public CatletMetadataChangeHandler(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore,
        Microsoft.Extensions.Logging.ILogger<CatletMetadataChangeHandler> logger)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task HandleChangeAsync(
        CatletMetadataChange change,
        CancellationToken cancellationToken = default)
    {
        var metadataId = change.MetadataId;
        var path = Path.Combine(_config.VirtualMachinesConfigPath, $"{metadataId}.json");

        var metadata = await _stateStore.For<CatletMetadata>()
            .GetByIdAsync(metadataId, cancellationToken);
        Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(_logger, "CTDIAG HANDLER metadata id={Id} found={Found}", metadataId, metadata is not null);
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
            SpecificationId = metadata.SpecificationId,
            SpecificationVersionId = metadata.SpecificationVersionId,
        };

        var json = CatletMetadataConfigModelJsonSerializer.Serialize(metadataConfig);
        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
