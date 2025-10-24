using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.ChangeTracking.Catlets;

internal class CatletSpecificationVersionChangeHandler : IChangeHandler<CatletSpecificationVersionChange>
{
    private readonly ChangeTrackingConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;

    public CatletSpecificationVersionChangeHandler(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        CatletSpecificationVersionChange change,
        CancellationToken cancellationToken = default)
    {
        var id = change.Id;
        var path = Path.Combine(_config.CatletSpecificationVersionsConfigPath, $"{id}.json");

        var specificationVersion
            = await _stateStore.For<CatletSpecificationVersion>()
            .GetByIdAsync(id, cancellationToken);
        if (specificationVersion is null)
        {
            _fileSystem.File.Delete(path);
            return;
        }

        var versionConfig = new CatletSpecificationVersionConfigModel
        {
            Id = specificationVersion.Id,
            SpecificationId = specificationVersion.SpecificationId,
            ContentType = specificationVersion.ContentType,
            Configuration = specificationVersion.Configuration,
            Comment = specificationVersion.Comment,
            CreatedAt = specificationVersion.CreatedAt,
        };

        var json = CatletSpecificationVersionConfigModelJsonSerializer.Serialize(versionConfig);
        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
