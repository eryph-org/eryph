using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.StateDb.Specifications;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Networks;
using Eryph.Modules.Controller.Serializers;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Seeding;

internal class CatletSpecificationSeeder : SeederBase
{
    private readonly string _specificationVersionsConfigPath;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStoreRepository<CatletSpecification> _specificationRepository;
    private readonly IStateStoreRepository<CatletSpecificationVersion> _specificationVersionRepository;

    public CatletSpecificationSeeder(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStoreRepository<CatletSpecification> specificationRepository,
        IStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository)
        : base(fileSystem, config.CatletSpecificationsConfigPath)
    {
        _specificationVersionsConfigPath = config.CatletSpecificationVersionsConfigPath;
        _fileSystem = fileSystem;
        _specificationRepository = specificationRepository;
        _specificationVersionRepository = specificationVersionRepository;
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
            Architecture = config.Architecture,
        };

        await _specificationRepository.AddAsync(specification, cancellationToken);
        await _specificationRepository.SaveChangesAsync(cancellationToken);
    }
}
