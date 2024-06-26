using System.Data;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.GenePool.Model;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using Moq;
using Xunit;

namespace Eryph.VmManagement.Test;

public class BreedAndFeedTests
{
    public static readonly IEnumerable<string> genesets =
    [
        "dbosoft/utt/latest",
        "dbosoft/UTT/Latest",
        "dbosoft/utt/1.0",
        "dbosoft/UTT/1.0",
    ];

    [Theory, CombinatorialData]
    public void Child_resolves_drive_config_from_parent(
        [CombinatorialMemberData(nameof(genesets))] string geneset)
    {
        var config = new CatletConfig
        {
            Name = "test",
            Parent = "parent",
        };

        var parentConfig = new CatletConfig
        {
            Name = "parent",
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = $"gene:{geneset}:sda"
                }
            ]
        };

        var genepoolReader = new Mock<ILocalGenepoolReader>();

        genepoolReader.Setup(x => x.GetGenesetReference(It.IsAny<GeneSetIdentifier>()))
            .Returns((GeneSetIdentifier id) =>
                id.Value == "dbosoft/utt/latest"
                    ? GeneSetIdentifier.New("dbosoft/utt/1.0")
                    : Option<GeneSetIdentifier>.None);

        var result = config.BreedAndFeed(genepoolReader.Object, parentConfig);

        result.Should().BeRight().Which.Drives.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:dbosoft/utt/1.0:sda"));
    }

    [Fact]
    public void Fodder_from_parent_has_informational_source()
    {
        var config = new CatletConfig
        {
            Name = "test",
            Parent = "dbosoft/parent/1.0",
        };

        var parentConfig = new CatletConfig
        {
            Name = "parent",
            Fodder =
            [
                new FodderConfig()
                {
                    Name = "parent-fodder",
                    Content = "parent fodder content",
                }
            ]
        };

        var genepoolReader = new Mock<ILocalGenepoolReader>();

        var result = config.BreedAndFeed(genepoolReader.Object, parentConfig);

        result.Should().BeRight().Which.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("parent-fodder");
                fodder.Source.Should().Be("gene:dbosoft/parent/1.0:catlet");
                fodder.Content.Should().Be("parent fodder content");
            });

        // The informational fodder source for fodder taken from the parent
        // must not be resolved (as no fodder gene actually exists).
        genepoolReader.Verify(x => x.GetGenesetReference(It.IsAny<GeneSetIdentifier>()), Times.Never);
        genepoolReader.Verify(x => x.ReadGeneContent(GeneType.Fodder, It.IsAny<GeneIdentifier>()), Times.Never);
    }

    [Theory, CombinatorialData]
    public void Child_uses_fodder_variable_binding(
        bool? catletSecret,
        [CombinatorialValues("false", "true")] string geneSecret)
    {
        var config = new CatletConfig
        {
            Name = "test",
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:dbosoft/utt/1.0:gene1",
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

        var genepoolReader = new Mock<ILocalGenepoolReader>();

        genepoolReader.Setup(x => x.GetGenesetReference(It.IsAny<GeneSetIdentifier>()))
            .Returns(Option<GeneSetIdentifier>.None);
        genepoolReader.Setup(x => x.ReadGeneContent(GeneType.Fodder,
                new GeneIdentifier(GeneSetIdentifier.New("dbosoft/utt/1.0"), GeneName.New("gene1"))))
            .Returns(
                $$"""
                  {
                    "name": "gene1",
                    "variables": [
                      {
                        "name": "foodVariable",
                        "value": "gene value",
                        "type": "String",
                        "required": false,
                        "secret": "{{geneSecret}}"
                      }
                    ],
                    "fodder": [
                      {
                        "name": "food1",
                        "content": "test1"
                      }
                    ]
                  }
                  """
            );

        var result = config.BreedAndFeed(genepoolReader.Object, Option<CatletConfig>.None);

        var newConfig = result.Should().BeRight().Subject;

        newConfig.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("food1");
                fodder.Source.Should().Be("gene:dbosoft/utt/1.0:gene1");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("foodVariable");
                        variable.Value.Should().Be("catlet value");
                        variable.Secret.Should().Be(catletSecret | bool.Parse(geneSecret));
                    });
            });
    }

    [Theory, CombinatorialData]
    public void Child_uses_fodder_variable_binding_for_parent_fodder(
        bool? catletSecret,
        [CombinatorialValues("false", "true")] string geneSecret)
    {
        var parentConfig = new CatletConfig
        {
            Name = "parent",
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:dbosoft/utt/1.0:gene1",
                },
            ],
        };

        var config = new CatletConfig
        {
            Name = "test",
            Parent = "parent",
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:dbosoft/utt/1.0:gene1",
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

        var genepoolReader = new Mock<ILocalGenepoolReader>();

        genepoolReader.Setup(x => x.GetGenesetReference(It.IsAny<GeneSetIdentifier>()))
            .Returns(Option<GeneSetIdentifier>.None);
        genepoolReader.Setup(x => x.ReadGeneContent(GeneType.Fodder,
                new GeneIdentifier(GeneSetIdentifier.New("dbosoft/utt/1.0"), GeneName.New("gene1"))))
            .Returns(
                $$"""
                  {
                    "name": "gene1",
                    "variables": [
                      {
                        "name": "foodVariable",
                        "value": "gene value",
                        "type": "String",
                        "required": false,
                        "secret": "{{geneSecret}}"
                      }
                    ],
                    "fodder": [
                      {
                        "name": "food1",
                        "content": "test1"
                      }
                    ]
                  }
                  """
            );

        var result = config.BreedAndFeed(genepoolReader.Object, parentConfig);

        var newConfig = result.Should().BeRight().Subject;

        newConfig.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("food1");
                fodder.Source.Should().Be("gene:dbosoft/utt/1.0:gene1");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Name.Should().Be("foodVariable");
                        variable.Value.Should().Be("catlet value");
                        variable.Secret.Should().Be(catletSecret | bool.Parse(geneSecret));
                    });
            });
    }


    [Theory, CombinatorialData]
    public void Child_eats_food_source_from_parent(
        [CombinatorialMemberData(nameof(genesets))] string geneset)
    {
        var config = new CatletConfig
        {
            Name = "test",
            Parent = "parent",
        };

        var parentConfig = new CatletConfig
        {
            Name = "parent",
            Fodder =
            [
                new FodderConfig
                {
                    Source = $"gene:{geneset}:gene1"
                }
            ]
        };

        SetupAndVerify_With_Gene_Food(parentConfig, config, ["food1", "food2"]);
    }

    [Theory, CombinatorialData]
    public void Child_eats_food_from_source(
        [CombinatorialMemberData(nameof(genesets))] string geneset)
    {
        var config = new CatletConfig
        { 
            Name = "test",
            Parent = "parent",
            Fodder = new[]
            {
                new FodderConfig
                {
                    Source = $"gene:{geneset}:gene1"
                }
            }
        };

        var parentConfig = new CatletConfig
        {
            Name = "parent"
        };

        SetupAndVerify_With_Gene_Food(parentConfig, config, ["food1", "food2"]);
    }

    private static void SetupAndVerify_With_Gene_Food(CatletConfig parentConfig, CatletConfig childConfig, IEnumerable<string> food)
    {
        var genepoolReader = new Mock<ILocalGenepoolReader>();

        genepoolReader.Setup(x => x.GetGenesetReference(It.IsAny<GeneSetIdentifier>()))
            .Returns((GeneSetIdentifier id) =>
                id.Value == "dbosoft/utt/latest"
                    ? GeneSetIdentifier.New("dbosoft/utt/1.0")
                    : Option<GeneSetIdentifier>.None);
        genepoolReader.Setup(x => x.ReadGeneContent(GeneType.Fodder,
                new GeneIdentifier(GeneSetIdentifier.New("dbosoft/utt/1.0"), GeneName.New("gene1"))))
            .Returns(
                """
                    {
                        "name": "gene1",
                        "fodder": [
                            { "name": "food1", "content": "test1"},
                            { "name": "food2", "content": "test2" }
                        ]
                    }
                """
            );

        var result = childConfig.BreedAndFeed(genepoolReader.Object, parentConfig);

        result.Should().BeRight().Which.Fodder.Should()
            .Equal(food, (fodderConfig, foodName) => fodderConfig.Name == foodName);
    }


    [Theory, CombinatorialData]
    public void Child_eats_only_selected_food(
        [CombinatorialMemberData(nameof(genesets))] string geneset)
    {
        var config = new CatletConfig
        {
            Name = "test",
            Parent = "parent",
            Fodder =
            [
                new FodderConfig
                {
                    Source = $"gene:{geneset}:gene1",
                    Name = "food2"
                }
            ]
        };

        var parentConfig = new CatletConfig
        {
            Name = "parent"
        };

        SetupAndVerify_With_Gene_Food(parentConfig, config, ["food2"]);
    }

    [Theory, CombinatorialData]
    public void Child_eats_some_food_from_parent(
        [CombinatorialMemberData(nameof(genesets))] string parentGeneset,
        [CombinatorialMemberData(nameof(genesets))] string childGeneset)
    {
        var config = new CatletConfig
        {
            Name = "test",
            Parent = "parent",
            Fodder =
            [
                new FodderConfig
                {
                    Source = $"gene:{childGeneset}:gene1",
                    Name = "food1",
                    Remove = true
                }
            ]
        };

        var parentConfig = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Source = $"gene:{parentGeneset}:gene1"
                }
            ]
        };

        SetupAndVerify_With_Gene_Food(parentConfig, config, ["food2"]);
    }

    [Theory, CombinatorialData]
    public void Child_eats_no_food_from_parent(
        [CombinatorialMemberData(nameof(genesets))] string parentGeneset,
        [CombinatorialMemberData(nameof(genesets))] string childGeneset)
    {
        var config = new CatletConfig
        {
            Name = "test",
            Parent = "parent",
            Fodder =
            [
                new FodderConfig
                {
                    Source = $"gene:{childGeneset}:gene1",
                    Remove = true
                },
                new FodderConfig
                {
                    Name = "food3"
                }
            ]
        };

        var parentConfig = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Source = $"gene:{parentGeneset}:gene1"
                }
            ]
        };

        SetupAndVerify_With_Gene_Food(parentConfig, config, ["food3"]);
    }
}