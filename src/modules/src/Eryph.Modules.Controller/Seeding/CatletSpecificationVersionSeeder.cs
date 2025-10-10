using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
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
            ConfigYaml = config.ConfigYaml,
            Comment = config.Comment,
            CatletId = config.CatletId,
            CreatedAt = config.CreatedAt,
        };

        await _specificationVersionRepository.AddAsync(specificationVersion, cancellationToken);
        await _specificationVersionRepository.SaveChangesAsync(cancellationToken);
    }
}
