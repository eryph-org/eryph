using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Serializers;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.Controller.Seeding;

internal class CatletSpecificationSeeder : SeederBase
{
    private readonly IStateStoreRepository<CatletSpecification> _specificationRepository;

    public CatletSpecificationSeeder(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStoreRepository<CatletSpecification> specificationRepository)
        : base(fileSystem, config.CatletSpecificationsConfigPath)
    {
        _specificationRepository = specificationRepository;
    }

    protected override async Task SeedAsync(Guid entityId, string json, CancellationToken cancellationToken = default)
    {
        bool exists = await _specificationRepository.AnyAsync(
            new CatletSpecificationSpecs.GetByIdReadOnly(entityId),
            cancellationToken);
        if (exists)
            return;

        var config = CatletSpecificationConfigModelJsonSerializer.Deserialize(json);
        var specification = new CatletSpecification
        {
            Id = entityId,
            ProjectId = config.ProjectId,
            Name = config.Name,
            Environment = EryphConstants.DefaultEnvironmentName,
            ResourceType = ResourceType.CatletSpecification,
            Architectures = config.Architectures
                .Map(a => Architecture.New(a))
                .ToHashSet(),
        };

        await _specificationRepository.AddAsync(specification, cancellationToken);
        await _specificationRepository.SaveChangesAsync(cancellationToken);
    }
}
