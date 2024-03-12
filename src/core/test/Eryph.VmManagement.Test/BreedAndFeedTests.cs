using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
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
        [InlineData("dbosoft/utt/1.0")]
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
                Drives = new[]
                {
                    new CatletDriveConfig
                    {
                        Source = $"gene:{geneset}:sda"
                    }
                }
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
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/utt/1.0")]
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
                Fodder = new[]
                {
                    new FodderConfig
                    {
                        Source = $"gene:{geneset}:gene1"
                    }
                }
            };

            SetupAndVerify_With_Gene_Food(parentConfig, config, new[] { "food1", "food2" });

        }

        [Theory]
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/utt/1.0")]
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

            SetupAndVerify_With_Gene_Food(parentConfig, config, new [] {"food1", "food2"});

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
        [InlineData("dbosoft/utt/1.0")]
        public void Child_eats_only_selected_food(string geneset)
        {
            var config = new CatletConfig
            {
                Name = "test",
                Parent = "parent",
                Fodder = new[]
                {
                    new FodderConfig
                    {
                        Source = $"gene:{geneset}:gene1",
                        Name = "food2"
                    }
                }
            };

            var parentConfig = new CatletConfig
            {
                Name = "parent"
            };

            SetupAndVerify_With_Gene_Food(parentConfig, config, new[] { "food2" });

        }

        [Theory]
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/utt/1.0")]
        public void Child_eats_some_food_from_parent(string geneset)
        {
            var config = new CatletConfig
            {
                Name = "test",
                Parent = "parent",
                Fodder = new[]
                {
                    new FodderConfig
                    {
                        Source = $"gene:{geneset}:gene1",
                        Name = "food1",
                        Remove = true
                    }
                }
            };

            var parentConfig = new CatletConfig
            {
                Fodder = new[]
                {
                    new FodderConfig
                    {
                        Source = $"gene:{geneset}:gene1"
                    }
                }
            };

            SetupAndVerify_With_Gene_Food(parentConfig, config, new[] { "food2" });

        }

        [Theory]
        [InlineData("dbosoft/utt/latest")]
        [InlineData("dbosoft/utt/1.0")]
        public void Child_eats_no_food_from_parent(string geneset)
        {
            var config = new CatletConfig
            {
                Name = "test",
                Parent = "parent",
                Fodder = new[]
                {
                    new FodderConfig
                    {
                        Source = $"gene:{geneset}:gene1",
                        Remove = true
                    },
                    new FodderConfig
                    {
                        Name = "food3"
                    }
                }
            };

            var parentConfig = new CatletConfig
            {
                Fodder = new[]
                {
                    new FodderConfig
                    {
                        Source = "gene:dbosoft/utt/1.0:gene1"
                    }
                }
            };

            SetupAndVerify_With_Gene_Food(parentConfig, config, new[] { "food3" });

        }
    }
}
