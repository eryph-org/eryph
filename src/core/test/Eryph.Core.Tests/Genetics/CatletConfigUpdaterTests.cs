using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.Core.Genetics;

using static LanguageExt.Prelude;

namespace Eryph.Core.Tests.Genetics;

public class CatletConfigUpdaterTests
{
    [Fact]
    public void ApplyUpdate_ValidConfigs_UsesFodderAndVariablesFromOldConfig()
    {
        var oldConfig = new CatletConfig
        {
            Parent = "acme/acme-os/1.0",
            Cpu = new CatletCpuConfig { Count = 2 },
            Fodder =
            [
                new FodderConfig
                {
                    Name = "old-parent-test-fodder",
                    Content = "old parent test content",
                },
                new FodderConfig
                {
                    Name = "old-test-fodder",
                    Content = "old test content",
                },
            ],
            Variables =
            [
                new VariableConfig
                {
                    Name = "oldParentTestVariable",
                },
                new VariableConfig
                {
                    Name = "oldTestVariable"
                },
            ],
        };

        var newConfig = new CatletConfig
        {
            Parent = "acme/acme-os/1.0",
            Cpu = new CatletCpuConfig { Count = 3 },
            Fodder =
            [
                new FodderConfig
                {
                    Name = "parent-test-fodder",
                    Content = "parent test content",
                },
                new FodderConfig
                {
                    Name = "test-fodder",
                    Content = "test content",
                },
            ],
            Variables =
            [
                new VariableConfig
                {
                    Name = "parentTestVariable",
                },
                new VariableConfig
                {
                    Name = "testVariable",
                },
            ],
        };

        var result = CatletConfigUpdater.ApplyUpdate(oldConfig, Empty, newConfig, Empty)
            .Should().BeRight().Subject;

        result.Config.Cpu.Should().BeEquivalentTo(newConfig.Cpu);

        // TODO test merging of pinned genes

        // Fodder and variables should be taken from the old config as 
        // they cannot be changed after the creation of the catlet.
        result.Config.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("old-parent-test-fodder");
                fodder.Content.Should().Be("old parent test content");
            },
            fodder =>
            {
                fodder.Name.Should().Be("old-test-fodder");
                fodder.Content.Should().Be("old test content");
            });
        result.Config.Variables.Should().SatisfyRespectively(
            variable => variable.Name.Should().Be("oldParentTestVariable"),
            variable => variable.Name.Should().Be("oldTestVariable"));
    }

    [Fact]
    public void ApplyUpdate_ParentHasChanged_ReturnsError()
    {
        var oldConfig = new CatletConfig
        {
            Parent = "acme/acme-os/1.0",
        };

        var newConfig = new CatletConfig
        {
            Parent = "acme/acme-os/2.0",
        };

        CatletConfigUpdater.ApplyUpdate(oldConfig, Empty, newConfig, Empty)
            .Should().BeLeft().Which.Message
            .Should().Be("The catlet's parent cannot be changed during an update.");
    }
}
