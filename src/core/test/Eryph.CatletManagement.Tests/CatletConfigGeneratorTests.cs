using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.StateDb.Model;
using FluentAssertions;

using static LanguageExt.Prelude;

namespace Eryph.CatletManagement.Tests;

public class CatletConfigGeneratorTests
{
    [Fact]
    public void Generate_DynamicMemoryDisabled_ReturnsConfigWithMinAndMaxMemory()
    {
        var catlet = new Catlet
        {
            Name = "test-catlet",
            Project = new Project
            {
                Id = EryphConstants.DefaultProjectId,
                Name = EryphConstants.DefaultProjectName,
            },
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            StartupMemory = 1024 * 1024 * 1024L,
            MinimumMemory = 512 * 1024 * 1024L,
            MaximumMemory = 2024 * 1024 * 1024L,
        };

        var result = CatletConfigGenerator.Generate(catlet, Empty, new CatletConfig());

        result.Memory.Should().NotBeNull();
        result.Memory!.Startup.Should().Be(1024);
        result.Memory.Minimum.Should().BeNull();
        result.Memory.Maximum.Should().BeNull();
    }
}
