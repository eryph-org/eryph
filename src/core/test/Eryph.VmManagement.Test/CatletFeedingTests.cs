using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Variables;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Resources.Machines;
using Eryph.VmManagement.TestBase;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt.Common;
using Moq;
using Xunit;

namespace Eryph.VmManagement.Test;

public class CatletFeedingTests
{
    private readonly Mock<ILocalGenepoolReader> _genepoolReaderMock = new();

    [Fact]
    public void Feed_AllFoodShouldBeEaten_EatsAllFood()
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Source = $"gene:acme/acme-tools/1.0:test-fodder",
                },
                new FodderConfig()
                {
                    Name = "catlet food",
                    Content = "catlet food content",
                },
            ],
        };

        ArrangeFood();

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("food1");
                fodder.Content.Should().Be("food 1 content");
                fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder");
            },
            fodder =>
            {
                fodder.Name.Should().Be("food2");
                fodder.Content.Should().Be("food 2 content");
                fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder");
            },
            fodder =>
            {
                fodder.Name.Should().Be("catlet food");
                fodder.Content.Should().Be("catlet food content");
                fodder.Source.Should().BeNull();
            });
    }

    [Fact]
    public void Feed_SomeFoodShouldBeEaten_EatsOnlySpecificFood()
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "food1",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                },
                new FodderConfig()
                {
                    Name = "catlet food",
                    Content = "catlet food content",
                },
            ],
        };

        ArrangeFood();

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("food1");
                fodder.Content.Should().Be("food 1 content");
                fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder");
            },
            fodder =>
            {
                fodder.Name.Should().Be("catlet food");
                fodder.Content.Should().Be("catlet food content");
                fodder.Source.Should().BeNull();
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("food1")]
    public void Feed_FoodShouldNotBeEaten_DoesNotEatFood(string? foodName)
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = foodName,
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                },
                new FodderConfig()
                {
                    Name = "catlet food",
                    Content = "catlet food content",
                },
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Remove = true,
                },
            ],
        };

        ArrangeFood();

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("catlet food");
                fodder.Content.Should().Be("catlet food content");
                fodder.Source.Should().BeNull();
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("food2")]
    public void Feed_SomeFoodShouldNotBeEaten_EatsRemainingFood(string? foodName)
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = foodName,
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                },
                new FodderConfig()
                {
                    Name = "catlet food",
                    Content = "catlet food content",
                },
                new FodderConfig()
                {
                    Name = "food1",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Remove = true,
                },
            ],
        };

        ArrangeFood();

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("food2");
                fodder.Content.Should().Be("food 2 content");
                fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder");
            },
            fodder =>
            {
                fodder.Name.Should().Be("catlet food");
                fodder.Content.Should().Be("catlet food content");
                fodder.Source.Should().BeNull();
            });
    }

    private void ArrangeFood()
    {
        _genepoolReaderMock.SetupGenesetReferences([]);

        _genepoolReaderMock.SetupFodderGene(
            "gene:acme/acme-tools/1.0:test-fodder",
            new FodderGeneConfig()
            {
                Name = "test-fodder",
                Fodder = 
                [
                    new FodderConfig()
                    {
                        Name = "food1",
                        Content = "food 1 content",
                    },
                    new FodderConfig()
                    {
                        Name = "food2",
                        Content = "food 2 content",
                    }
                ]
            });
    }

    [Theory, CombinatorialData]
    public void Feed_FodderVariableIsBound_BoundVariableIsUsed(
        bool? catletSecret,
        bool? geneSecret)
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "foodVariable",
                            Value = "catlet value",
                            Secret = catletSecret,
                        },
                    ],
                },
            ],
        };

        _genepoolReaderMock.SetupGenesetReferences([]);
        _genepoolReaderMock.SetupFodderGene(
            "gene:acme/acme-tools/1.0:test-fodder",
            new FodderGeneConfig
            {
                Name = "test-fodder",
                Variables =
                [
                    new VariableConfig()
                    {
                        Name = "foodVariable",
                        Value = "gene value",
                        Type = VariableType.String,
                        Required = false,
                        Secret = geneSecret,
                    },
                ],
                Fodder =
                [
                    new FodderConfig()
                    {
                        Name = "food1",
                        Content = "test food content",
                    },
                ],
            });

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.Object);

        var newConfig = result.Should().BeRight().Subject;

        newConfig.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("food1");
                fodder.Content.Should().Be("test food content");
                fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("foodVariable");
                        variable.Value.Should().Be("catlet value");
                        variable.Secret.Should().Be(catletSecret | geneSecret);
                    });
            });
    }

    [Fact]
    public void Feed_FodderWithoutSource_FodderIsNotTouched()
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "test-fodder",
                    Content = "test food content",
                },
            ],
        };

        _genepoolReaderMock.SetupGenesetReferences([]);

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("test-fodder");
                fodder.Content.Should().Be("test food content");
                fodder.Source.Should().BeNull();
            });

        _genepoolReaderMock.Verify(x => x.GetGenesetReference(It.IsAny<GeneSetIdentifier>()), Times.Never);
        _genepoolReaderMock.Verify(x => x.ReadGeneContent(GeneType.Fodder, It.IsAny<GeneIdentifier>()), Times.Never);
    }

    [Fact]
    public void Feed_FodderWithInformationalSource_FodderSourceIsNotResolvedInGenepool()
    {
        var config = new CatletConfig
        {
            Name = "test",
            Parent = "acme/acme-os/1.0",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "parent-fodder",
                    Content = "parent fodder content",
                    Source = "gene:acme/acme-os/1.0:catlet",
                },
            ],
        };

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("parent-fodder");
                fodder.Source.Should().Be("gene:acme/acme-os/1.0:catlet");
                fodder.Content.Should().Be("parent fodder content");
            });

        // The informational fodder source for fodder taken from the parent
        // must not be resolved (as no fodder gene actually exists).
        _genepoolReaderMock.Verify(x => x.GetGenesetReference(It.IsAny<GeneSetIdentifier>()), Times.Never);
        _genepoolReaderMock.Verify(x => x.ReadGeneContent(GeneType.Fodder, It.IsAny<GeneIdentifier>()), Times.Never);
    }

    [Fact]
    public void Feed_FodderWithUnresolvedSource_ReturnsError()
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/latest:test-fodder",
                },
            ],
        };

        _genepoolReaderMock.SetupGenesetReferences(new Dictionary<string, string>
        {
            ["acme/acme-tools/latest"] = "acme/acme-tools/1.0",
        });

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.Object);

        result.Should().BeLeft().Which.Message
            .Should().Be("The gene 'gene:acme/acme-tools/latest:test-fodder' is an unresolved reference. This should not happen.");
    }

    [Fact]
    public void Feed_FodderNotInGenepool_ReturnsError()
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                },
            ],
        };

        _genepoolReaderMock.SetupGenesetReferences([]);
        _genepoolReaderMock.Setup(m => m.ReadGeneContent(
            GeneType.Fodder, GeneIdentifier.New("gene:acme/acme-tools/1.0:test-fodder")))
            .Returns(Error.New("Gene 'gene:acme/acme-tools/1.0:test-fodder' does not exist in local genepool."));

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.Object);

        result.Should().BeLeft().Which.Message
            .Should().Be("Gene 'gene:acme/acme-tools/1.0:test-fodder' does not exist in local genepool.");
    }

    [Fact]
    public void FeedSystemVariables_SystemVariablesAreAppended()
    {
        var catletId = Guid.NewGuid();
        var vmId = Guid.NewGuid();

        var metadata = new CatletMetadata()
        {
            MachineId = catletId,
            VMId = vmId,
        };

        var config = new CatletConfig();

        var result = CatletFeeding.FeedSystemVariables(config, metadata);

        result.Variables.Should().SatisfyRespectively(
            variable =>
            {
                variable.Name.Should().Be(EryphConstants.SystemVariables.CatletId);
                variable.Value.Should().Be(catletId.ToString());
                variable.Type.Should().Be(VariableType.String);
                variable.Required.Should().BeFalse();
                variable.Secret.Should().BeFalse();
            },
            variable =>
            {
                variable.Name.Should().Be(EryphConstants.SystemVariables.VmId);
                variable.Value.Should().Be(catletId.ToString());
                variable.Type.Should().Be(VariableType.String);
                variable.Required.Should().BeFalse();
                variable.Secret.Should().BeFalse();
            });
    }
}
