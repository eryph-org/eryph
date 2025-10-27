using System;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;

namespace Eryph.Modules.Controller.Seeding;

[UsedImplicitly]
internal class CatletSpecificationVersionSeeder : SeederBase
{
    private readonly IStateStoreRepository<CatletSpecificationVersion> _specificationVersionRepository;

    public CatletSpecificationVersionSeeder(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository)
        : base(fileSystem, config.CatletSpecificationVersionsConfigPath)
    {
        _specificationVersionRepository = specificationVersionRepository;
    }

    protected override async Task SeedAsync(Guid entityId, string json, CancellationToken cancellationToken = default)
    {
        bool exists = await _specificationVersionRepository.AnyAsync(
            new CatletSpecificationVersionSpecs.GetByIdReadOnly(entityId),
            cancellationToken);
        if (exists)
            return;

        var config = CatletSpecificationVersionConfigModelJsonSerializer.Deserialize(json);
        var specificationVersion = new CatletSpecificationVersion
        {
            Id = entityId,
            SpecificationId = config.SpecificationId,
            Architectures = config.Architectures
                .Map(a => Architecture.New(a))
                .ToHashSet(),
            ContentType = config.ContentType,
            Configuration = config.OriginalConfig,
            Comment = config.Comment,
            CreatedAt = config.CreatedAt,
            Variants = config.Variants
                .Map(v =>
                {
                    var builtConfig = JsonSerializer.Serialize(v.BuiltConfig, CatletConfigJsonSerializer.Options);
                    return new CatletSpecificationVersionVariant
                    {
                        Id = v.Id,
                        SpecificationVersionId = v.SpecificationVersionId,
                        Architecture = Architecture.New(v.Architecture),
                        BuiltConfig = builtConfig,
                        PinnedGenes = v.PinnedGenes
                            .Map(kvp => (UniqueGeneIdentifier.New(kvp.Key), GeneHash.New(kvp.Value)))
                            .ToDictionary()
                            .ToGenesList(entityId),
                    };
                }).ToList()

        };

        await _specificationVersionRepository.AddAsync(specificationVersion, cancellationToken);
        await _specificationVersionRepository.SaveChangesAsync(cancellationToken);
    }
}
