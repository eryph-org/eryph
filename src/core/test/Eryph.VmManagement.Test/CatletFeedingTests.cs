using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Variables;
using Eryph.Core.Genetics;
using Eryph.VmManagement.TestBase;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt.Common;
using Moq;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;

public class CatletFeedingTests
{
    private readonly MockGenePoolReader _genepoolReaderMock = new();

    [Theory, CombinatorialData]
    public void Feed_AllFoodShouldBeEaten_EatsAllFood(
        [CombinatorialValues(null, "gene:acme/acme-os/1.0:catlet")]
        string? catletSource,
        [CombinatorialValues("any", "hyperv/any", "hyperv/amd64")]
        string architecture)
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
                new FodderConfig()
                {
                    Name = "catlet-food",
                    Content = "catlet food content",
                    Source = catletSource,
                },
            ],
        };

        ArrangeFood(architecture);

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.ResolvedGenes, _genepoolReaderMock.Object);

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
                fodder.Name.Should().Be("catlet-food");
                fodder.Content.Should().Be("catlet food content");
                fodder.Source.Should().Be(catletSource);
            });
    }

    [Theory, CombinatorialData]
    public void Feed_SomeFoodShouldBeEaten_EatsOnlySpecificFood(
        [CombinatorialValues(null, "gene:acme/acme-os/1.0:catlet")]
        string? catletSource,
        [CombinatorialValues("any", "hyperv/any", "hyperv/amd64")]
        string architecture)
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
                    Name = "catlet-food",
                    Content = "catlet food content",
                    Source = catletSource,
                },
            ],
        };

        ArrangeFood(architecture);

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.ResolvedGenes, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("food1");
                fodder.Content.Should().Be("food 1 content");
                fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder");
            },
            fodder =>
            {
                fodder.Name.Should().Be("catlet-food");
                fodder.Content.Should().Be("catlet food content");
                fodder.Source.Should().Be(catletSource);
            });
    }

    [Theory, CombinatorialData]
    public void Feed_FoodShouldNotBeEaten_DoesNotEatFood(
        [CombinatorialValues(null, "food1")]
        string? foodName,
        [CombinatorialValues(null, "gene:acme/acme-os/1.0:catlet")]
        string? catletSource,
        [CombinatorialValues("any", "hyperv/any", "hyperv/amd64")]
        string architecture)
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
                    Name = "catlet-food",
                    Content = "catlet food content",
                    Source = catletSource,
                },
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Remove = true,
                },
            ],
        };

        ArrangeFood(architecture);

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.ResolvedGenes, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("catlet-food");
                fodder.Content.Should().Be("catlet food content");
                fodder.Source.Should().Be(catletSource);
            });
    }

    [Theory, CombinatorialData]
    public void Feed_SomeFoodShouldNotBeEaten_EatsRemainingFood(
        [CombinatorialValues(null, "food2")]
        string? foodName,
        [CombinatorialValues(null, "gene:acme/acme-os/1.0:catlet")]
        string? catletSource,
        [CombinatorialValues("any", "hyperv/any", "hyperv/amd64")]
        string architecture)
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
                    Name = "catlet-food",
                    Content = "catlet food content",
                    Source = catletSource,
                },
                new FodderConfig()
                {
                    Name = "food1",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Remove = true,
                },
            ],
        };

        ArrangeFood(architecture);

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.ResolvedGenes, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("food2");
                fodder.Content.Should().Be("food 2 content");
                fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder");
            },
            fodder =>
            {
                fodder.Name.Should().Be("catlet-food");
                fodder.Content.Should().Be("catlet food content");
                fodder.Source.Should().Be(catletSource);
            });
    }

    [Theory, CombinatorialData]
    public void Feed_FoodExistsMultipleTimes_FoodIsMerged(
        [CombinatorialValues("any", "hyperv/any", "hyperv/amd64")]
        string architecture)
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
                            Name = "geneVariable",
                            Value = "first value",
                        },
                    ],
                },
                new FodderConfig()
                {
                    Name = "food1",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                    Variables =
                    [
                        new VariableConfig()
                        {
                            Name = "geneVariable",
                            Value = "second value",
                        },
                    ],
                },
            ],
        };

        ArrangeFood(architecture);

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.ResolvedGenes, _genepoolReaderMock.Object);
        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("food1");
                fodder.Content.Should().Be("food 1 content");
                fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("geneVariable");
                        variable.Value.Should().Be("second value");
                    });
            },
            fodder =>
            {
                fodder.Name.Should().Be("food2");
                fodder.Content.Should().Be("food 2 content");
                fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("geneVariable");
                        variable.Value.Should().Be("first value");
                    });
            });
    }

    private void ArrangeFood(string architecture)
    {
        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);
        _genepoolReaderMock.SetupFodderGene(
            "gene:acme/acme-tools/1.0:test-fodder",
            architecture,
            new FodderGeneConfig()
            {
                Name = "test-fodder",
                Variables =
                [
                    new VariableConfig()
                    {
                        Name = "geneVariable",
                    }
                ],
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
        bool? geneSecret,
        [CombinatorialValues("any", "hyperv/any", "hyperv/amd64")]
        string architecture)
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

        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);
        _genepoolReaderMock.SetupFodderGene(
            "gene:acme/acme-tools/1.0:test-fodder",
            architecture,
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

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.ResolvedGenes, _genepoolReaderMock.Object);

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
    public void Feed_VariableBindingWithoutVariableInFodder_ReturnsError()
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
                            Name = "missingVariable",
                            Value = "catlet value",
                        },
                    ],
                },
            ],
        };

        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);
        _genepoolReaderMock.SetupFodderGene(
            "gene:acme/acme-tools/1.0:test-fodder",
            "any",
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

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.ResolvedGenes, _genepoolReaderMock.Object);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not expand the fodder gene 'gene:acme/acme-tools/1.0:test-fodder'.");
        error.Inner.Should().BeSome().Which.Message
            .Should().Be("Found a binding for the variable 'missingVariable' but the variable is not defined in the fodder gene.");
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

        var result = CatletFeeding.Feed(config, Empty, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("test-fodder");
                fodder.Content.Should().Be("test food content");
                fodder.Source.Should().BeNull();
            });

        _genepoolReaderMock.Verify(
            x => x.GetReferencedGeneSet(
                It.IsAny<GeneSetIdentifier>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _genepoolReaderMock.Verify(
            x => x.GetGeneContent(
                It.IsAny<UniqueGeneIdentifier>(),
                It.IsAny<GeneHash>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Feed_FodderWithInformationalSource_FodderSourceIsNotResolvedInGenePool()
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

        var result = CatletFeeding.Feed(config, Empty, _genepoolReaderMock.Object);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("parent-fodder");
                fodder.Source.Should().Be("gene:acme/acme-os/1.0:catlet");
                fodder.Content.Should().Be("parent fodder content");
            });

        // The informational fodder source for fodder taken from the parent
        // must not be resolved (as no fodder gene actually exists).
        _genepoolReaderMock.Verify(
            x => x.GetReferencedGeneSet(
                It.IsAny<GeneSetIdentifier>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _genepoolReaderMock.Verify(
            x => x.GetGeneContent(
                It.IsAny<UniqueGeneIdentifier>(),
                It.IsAny<GeneHash>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Feed_FodderWithUnresolvedGeneInSource_ReturnsError()
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

        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/latest", "acme/acme-tools/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);

        var result = CatletFeeding.Feed(config, Empty, _genepoolReaderMock.Object);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not expand the fodder gene 'gene:acme/acme-tools/1.0:test-fodder'.");
        error.Inner.Should().BeSome().Which.Message
            .Should().Be("The gene 'gene:acme/acme-tools/1.0:test-fodder' has not been correctly resolved. This should not happen.");
    }

    [Fact]
    public void Feed_FodderWithUnresolvedGeneSetInSource_ReturnsError()
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

        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/latest", "acme/acme-tools/1.0");

        var result = CatletFeeding.Feed(config, Empty, _genepoolReaderMock.Object);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not expand the fodder gene 'gene:acme/acme-tools/latest:test-fodder'.");
        error.Inner.Should().BeSome().Which.Message
            .Should().Be("The gene 'gene:acme/acme-tools/latest:test-fodder' is an unresolved reference. This should not happen.");
    }

    [Theory, CombinatorialData]
    public void Feed_FodderNotInGenePool_ReturnsError(
        [CombinatorialValues("any", "hyperv/any", "hyperv/amd64")]
        string architecture)
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

        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);

        var uniqueGeneId = new UniqueGeneIdentifier(
            GeneType.Fodder,
            GeneIdentifier.New("gene:acme/acme-tools/1.0:test-fodder"),
            Architecture.New(architecture));
        var geneHash = GeneHash.New("sha256:4242424242424242424242424242424242424242424242424242424242424242");

        _genepoolReaderMock.Setup(m => m.GetGeneContent(uniqueGeneId, geneHash, It.IsAny<CancellationToken>()))
            .Returns(Error.New("Gene 'gene:acme/acme-tools/1.0:test-fodder' does not exist in local genepool."));

        var resolvedGenes = HashMap((uniqueGeneId, geneHash));

        var result = CatletFeeding.Feed(config, resolvedGenes, _genepoolReaderMock.Object);
        
        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not expand the fodder gene 'gene:acme/acme-tools/1.0:test-fodder'.");
        error.Inner.Should().BeSome().Which.Message
            .Should().Be("Gene 'gene:acme/acme-tools/1.0:test-fodder' does not exist in local genepool.");
    }

    [Fact]
    public void Feed_FoodDoesNotExistInFodderGene_ReturnsError()
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "food2",
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                },
            ],
        };

        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);

        _genepoolReaderMock.SetupFodderGene(
            "gene:acme/acme-tools/1.0:test-fodder",
            "hyperv/amd64",
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
                ]
            });

        var result = CatletFeeding.Feed(config, _genepoolReaderMock.ResolvedGenes, _genepoolReaderMock.Object);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not expand the fodder gene 'gene:acme/acme-tools/1.0:test-fodder'.");
        error.Inner.Should().BeSome().Which.Message
            .Should().Match("The food 'food2' does not exist in the gene fodder::gene:acme/acme-tools/1.0:test-fodder[hyperv/amd64] (sha256:*).");
    }
}
