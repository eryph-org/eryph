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

internal class CatletSpecificationVersionChangeHandler(
    ChangeTrackingConfig config,
    IFileSystem fileSystem,
    IStateStore stateStore)
    : IChangeHandler<CatletSpecificationVersionChange>
{
    public async Task HandleChangeAsync(
        CatletSpecificationVersionChange change,
        CancellationToken cancellationToken = default)
    {
        var id = change.Id;
        var path = Path.Combine(config.CatletSpecificationVersionsConfigPath, $"{id}.json");

        var specificationVersion
            = await stateStore.For<CatletSpecificationVersion>()
                .GetByIdAsync(id, cancellationToken);
        if (specificationVersion is null)
        {
            fileSystem.File.Delete(path);
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
            Comment = specificationVersion.Comment ?? "",
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
                .ToList(),
        };

        var json = CatletSpecificationVersionConfigModelJsonSerializer.Serialize(versionConfig);
        await fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
