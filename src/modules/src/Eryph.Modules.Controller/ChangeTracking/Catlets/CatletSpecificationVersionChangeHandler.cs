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
    private readonly Microsoft.Extensions.Logging.ILogger<CatletSpecificationVersionChangeHandler> _logger;

    public CatletSpecificationVersionChangeHandler(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore,
        Microsoft.Extensions.Logging.ILogger<CatletSpecificationVersionChangeHandler> logger)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
        _logger = logger;
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
        Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(_logger, "CTDIAG VersionHandler id={Id} found={Found}", id, specificationVersion is not null);
        if (specificationVersion is null)
        {
            _fileSystem.File.Delete(path);
            return;
        }

        var versionConfig = new CatletSpecificationVersionConfigModel
        {
            Id = specificationVersion.Id,
            SpecificationId = specificationVersion.SpecificationId,
            Architectures = specificationVersion.Architectures
                .Map(a => a.Value)
                .ToHashSet(),
            ContentType = specificationVersion.ContentType,
            OriginalConfig = specificationVersion.Configuration,
            Comment = specificationVersion.Comment,
            CreatedAt = specificationVersion.CreatedAt,
            Variants = specificationVersion.Variants
                .Map(v =>
                {
                    using var jsonDocument = JsonDocument.Parse(v.BuiltConfig);

                    return new CatletSpecificationVersionVariantConfigModel
                    {
                        Id = v.Id,
                        SpecificationVersionId = v.SpecificationVersionId,
                        Architecture = v.Architecture.Value,
                        BuiltConfig = jsonDocument.RootElement.Clone(),
                        PinnedGenes = v.PinnedGenes
                            .ToGenesDictionary()
                            .Map(kvp => (kvp.Key.Value, kvp.Value.Value))
                            .ToDictionary(),
                    };
                })
                .ToList()
        };

        var json = CatletSpecificationVersionConfigModelJsonSerializer.Serialize(versionConfig);
        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
        Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(_logger, "CTDIAG VersionHandler wrote file id={Id} path={Path}", id, path);
    }
}
