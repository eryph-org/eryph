using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Variables;
using Eryph.Core;
using Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Catlets;
using Eryph.StateDb;
using Xunit.Abstractions;
using Architecture = Eryph.Core.Genetics.Architecture;
using CatletSpecification = Eryph.StateDb.Model.CatletSpecification;
using CatletSpecificationVersion = Eryph.StateDb.Model.CatletSpecificationVersion;
using CatletSpecificationVersionVariant = Eryph.StateDb.Model.CatletSpecificationVersionVariant;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.CatletSpecifications;

/// <summary>
/// Seeds a specification with variables and fodder and a second specification
/// without fodder.
/// </summary>
public abstract class CatletSpecificationTestBase(ITestOutputHelper outputHelper)
    : CatletTestBase(outputHelper)
{
    protected static readonly Guid SpecificationId = Guid.NewGuid();
    protected static readonly Guid SpecificationVersionId = Guid.NewGuid();
    protected static readonly Guid EmptySpecificationId = Guid.NewGuid();
    protected static readonly Guid EmptySpecificationVersionId = Guid.NewGuid();

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await base.SeedAsync(stateStore);

        await AddSpecification(stateStore, SpecificationId, SpecificationVersionId, "export", new CatletConfig
        {
            Name = "export",
            Variables =
            [
                new VariableConfig { Name = "userName", Value = "alice" },
                new VariableConfig { Name = "password", Required = true, Secret = true },
                new VariableConfig { Name = "port", Type = VariableType.Number, Value = "22" },
            ],
            Fodder =
            [
                new FodderConfig
                {
                    Name = "setup",
                    Type = "shellscript",
                    Filename = "setup.sh",
                    Content = "user={{ userName }}",
                },
                new FodderConfig { Name = "catlet", Content = "catlet={{ catletId }}" },
                new FodderConfig { Name = "vm", Content = "vm={{ vmId }}" },
                new FodderConfig { Name = "secret", Content = "pw={{ password }}", Secret = true },
            ],
        });

        await AddSpecification(stateStore, EmptySpecificationId, EmptySpecificationVersionId, "empty",
            new CatletConfig { Name = "empty" });
    }

    private static async Task AddSpecification(
        IStateStore stateStore,
        Guid specificationId,
        Guid specificationVersionId,
        string name,
        CatletConfig builtConfig)
    {
        var architecture = Architecture.New(EryphConstants.DefaultArchitecture);
        await stateStore.For<CatletSpecification>().AddAsync(new CatletSpecification
        {
            Id = specificationId,
            ProjectId = EryphConstants.DefaultProjectId,
            Name = name,
            Architectures = new HashSet<Architecture>([architecture]),
            Environment = EryphConstants.DefaultEnvironmentName,
            Versions =
            [
                new CatletSpecificationVersion
                {
                    Id = specificationVersionId,
                    SpecificationId = specificationId,
                    ContentType = "application/json",
                    Configuration = CatletConfigJsonSerializer.Serialize(builtConfig),
                    Architectures = new HashSet<Architecture>([architecture]),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Variants =
                    [
                        new CatletSpecificationVersionVariant
                        {
                            Id = Guid.NewGuid(),
                            SpecificationVersionId = specificationVersionId,
                            Architecture = architecture,
                            BuiltConfig = CatletConfigJsonSerializer.Serialize(builtConfig),
                            PinnedGenes = [],
                        },
                    ],
                },
            ],
        });
    }
}
