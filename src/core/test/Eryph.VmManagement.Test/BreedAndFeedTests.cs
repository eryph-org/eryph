using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.GenePool.Model;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using Moq;
using Xunit;

namespace Eryph.VmManagement.Test
{
    public class BreedAndFeedTests
    {
        [Theory]
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/UTT/Latest")]
        [InlineData("dbosoft/utt/1.0")]
        [InlineData("dbosoft/UTT/1.0")]
        public void Child_resolves_drive_config_from_parent(string geneset)
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

            result.Should().BeRight();
            var newConfig = result.IfLeft(() => null);

            newConfig.Should().NotBeNull();
            newConfig.Drives.Should().HaveCount(1);
            newConfig.Drives?.Should().ContainSingle(d => d.Source == "gene:dbosoft/utt/1.0:sda");
        }

        [Theory]
        [InlineData(null, "false")]
        [InlineData(false, "false")]
        [InlineData(true, "false")]
        [InlineData(null, "true")]
        [InlineData(false, "true")]
        [InlineData(true, "true")]
        public void Child_uses_fodder_variable_binding(bool? catletSecret, string geneSecret)
        {
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
                .Returns((GeneSetIdentifier id) => Option<GeneSetIdentifier>.None);
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
                    fodder.Variables.Should().SatisfyRespectively(
                        variable =>
                        {
                            variable.Name.Should().Be("foodVariable");
                            variable.Value.Should().Be("catlet value");
                            variable.Secret.Should().Be(catletSecret | bool.Parse(geneSecret));
                        });
                });
        }

        [Theory]
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/UTT/Latest")]
        [InlineData("dbosoft/utt/1.0")]
        [InlineData("dbosoft/UTT/1.0")]
        public void Child_eats_food_source_from_parent(string geneset)
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

        [Theory]
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/UTT/Latest")]
        [InlineData("dbosoft/utt/1.0")]
        [InlineData("dbosoft/UTT/1.0")]
        public void Child_eats_food_from_source(string geneset)
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

            result.Should().BeRight();
            var newConfig = result.IfLeft(() => null);

            newConfig.Should().NotBeNull();

            var foodList = food.ToArray();
            newConfig.Fodder.Should().HaveCount(foodList.Length);
            newConfig.Fodder?.Select(f => f.Name).Should().BeEquivalentTo(foodList);
        }


        [Theory]
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/UTT/Latest")]
        [InlineData("dbosoft/utt/1.0")]
        [InlineData("dbosoft/UTT/1.0")]
        public void Child_eats_only_selected_food(string geneset)
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

        [Theory]
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/UTT/Latest")]
        [InlineData("dbosoft/utt/1.0")]
        [InlineData("dbosoft/UTT/1.0")]
        public void Child_eats_some_food_from_parent(string geneset)
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
                        Source = $"gene:{geneset}:gene1"
                    }
                ]
            };

            SetupAndVerify_With_Gene_Food(parentConfig, config, ["food2"]);
        }

        [Theory]
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/UTT/latest")]
        [InlineData("dbosoft/utt/1.0")]
        [InlineData("dbosoft/UTT/1.0")]
        public void Child_eats_no_food_from_parent(string geneset)
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
                        Source = "gene:dbosoft/utt/1.0:gene1"
                    }
                ]
            };

            SetupAndVerify_With_Gene_Food(parentConfig, config, ["food3"]);
        }
    }
}
