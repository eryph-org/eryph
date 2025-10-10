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
            //LatestId = config.LatestId,
        };

        await _specificationRepository.AddAsync(specification, cancellationToken);
        await _specificationRepository.SaveChangesAsync(cancellationToken);

        // TODO Seed only the Latest reference and seed the other versions separately

        //await SeedVersionsAsync(entityId, cancellationToken);

        //specification.LatestId = config.LatestId;
        //await _specificationRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedVersionsAsync(Guid specificationId, CancellationToken cancellationToken)
    {
        var files = _fileSystem.Directory.EnumerateFiles(_specificationVersionsConfigPath, $"{specificationId}_*.json");
        foreach (var file in files)
        {
            try
            {
                _fileSystem.File.Copy(file, $"{file}.bak", true);
                var content = await _fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, cancellationToken);
                var entityId = Guid.Parse(_fileSystem.Path.GetFileNameWithoutExtension(file));
                await SeedVersionAsync(specificationId, entityId, content, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new SeederException($"Failed to seed database from file '{file}'", ex);
            }
        }
    }

    private async Task SeedVersionAsync(
        Guid specificationId,
        Guid entityId,
        string json,
        CancellationToken cancellationToken)
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
            SpecificationId = specificationId,
            ConfigYaml = config.ConfigYaml,
            CatletId = config.CatletId,
            CreatedAt = config.CreatedAt,
            IsDraft = config.IsDraft,
            // TODO add genes
        };

        await _specificationVersionRepository.AddAsync(specificationVersion, cancellationToken);
        await _specificationVersionRepository.SaveChangesAsync(cancellationToken);
    }
}
