using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller.Compute;
using Eryph.Resources.Machines;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Compute;

public class UpdateCatletSagaTests
{
    [Fact]
    public void PrepareGenes_ValidConfigs_UsesFodderAndVariablesFromMetadata()
    {
        var metaData = new CatletMetadata
        {
            Fodder =
            [
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
                    Name = "oldTestVariable"
                },
            ],
            Parent = "acme/acme-os/1.0",
            ParentConfig = new CatletConfig
            {
                Cpu = new CatletCpuConfig { Count = 2 },
                Fodder =
                [
                    new FodderConfig
                    {
                        Name = "old-parent-test-fodder",
                        Content = "old parent test content",
                    },
                ],
                Variables =
                [
                    new VariableConfig
                    {
                        Name = "oldParentTestVariable",
                    },
                ],
            },
        };

        var newConfig = new CatletConfig
        {
            Parent = "acme/acme-os/2.0",
            Fodder =
            [
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
                    Name = "testVariable",
                },
            ],
        };

        var newParentConfig = new CatletConfig
        {
            Cpu = new CatletCpuConfig { Count = 3 },
            Fodder =
            [
                new FodderConfig
                {
                    Name = "parent-test-fodder",
                    Content = "parent test content",
                },
            ],
            Variables =
            [
                new VariableConfig
                {
                    Name = "parentTestVariable",
                },
            ],
        };

        var geneSetMap = HashMap(
            (GeneSetIdentifier.New("acme/acme-os/2.0"), GeneSetIdentifier.New("acme/acme-os/2.0")));
        var parentsMap = HashMap(
            (GeneSetIdentifier.New("acme/acme-os/2.0"), newParentConfig));

        var result = UpdateCatletSaga.PrepareConfigs(newConfig, metaData, geneSetMap, parentsMap);

        var configs = result.Should().BeRight().Subject;

        configs.BredConfig.Cpu.Should().BeEquivalentTo(newParentConfig.Cpu);

        // Fodder and variables should be taken from the metadata as 
        // they cannot be changed after the creation of the catlet.
        configs.BredConfig.Fodder.Should().SatisfyRespectively(
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
        configs.BredConfig.Variables.Should().SatisfyRespectively(
            variable => variable.Name.Should().Be("oldParentTestVariable"),
            variable => variable.Name.Should().Be("oldTestVariable"));
    }
}
