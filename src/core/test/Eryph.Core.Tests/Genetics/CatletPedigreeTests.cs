using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core.Tests.Genetics;

public class CatletPedigreeTests
{
    private static readonly GeneSetIdentifier ParentId = new("acme/acme-parent/1.0");
    private static readonly GeneSetIdentifier ParentRefId = new("acme/acme-parent/latest");
    private static readonly GeneSetIdentifier GrandParentId = new("acme/acme-grand-parent/1.0");
    private static readonly GeneSetIdentifier GrandParentRefId = new("acme/acme-grand-parent/latest");

    [Fact]
    public void Breed_PedigreeContainsCircle_ReturnsFail()
    {
        var parentConfig = new CatletConfig
        {
            Parent = GrandParentId.Value,
        };

        var grandParentConfig = new CatletConfig
        {
            Parent = ParentId.Value,
        };

        var geneSetMap = HashMap((ParentId, ParentId), (GrandParentId, GrandParentId));
        var ancestors = HashMap((ParentId, parentConfig), (GrandParentId, grandParentConfig));

        var config = new CatletConfig
        {
            Parent = ParentId.Value,
        };

        var result = CatletPedigree.Breed(config, geneSetMap, ancestors);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be(
            "Could not breed ancestor in the pedigree catlet "
            + "-> acme/acme-parent/1.0 "
            + "-> acme/acme-grand-parent/1.0 "
            + "-> acme/acme-parent/1.0.");
        error.Inner.Should().BeSome().Which.Message
            .Should().Be("The pedigree contains a circle.");
    }

    [Fact]
    public void Breed_PedigreeContainsCircleWithReferences_ReturnsFail()
    {
        var parentConfig = new CatletConfig
        {
            Parent = GrandParentRefId.Value,
        };

        var grandParentConfig = new CatletConfig
        {
            Parent = ParentRefId.Value,
        };

        var geneSetMap = HashMap((ParentRefId, ParentId), (GrandParentRefId, GrandParentId));
        var ancestors = HashMap((ParentId, parentConfig), (GrandParentId, grandParentConfig));

        var config = new CatletConfig
        {
            Parent = ParentRefId.Value,
        };

        var result = CatletPedigree.Breed(config, geneSetMap, ancestors);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be(
            "Could not breed ancestor in the pedigree catlet "
            + "-> (acme/acme-parent/latest -> acme/acme-parent/1.0) "
            + "-> (acme/acme-grand-parent/latest -> acme/acme-grand-parent/1.0) "
            + "-> (acme/acme-parent/latest -> acme/acme-parent/1.0).");
        error.Inner.Should().BeSome().Which.Message
            .Should().Be("The pedigree contains a circle.");
    }

    [Fact]
    public void Breed_LeftoverRemoveMutations_ReturnsConfigWithoutRemoveMutations()
    {
        var config = new CatletConfig
        {
            Capabilities =
            [
                new CatletCapabilityConfig
                {
                    Name = "test_capability",
                    Mutation = MutationType.Remove,
                }
            ],
            Drives =
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                    Mutation = MutationType.Remove,
                }
            ],
            Networks =
            [
                new CatletNetworkConfig
                {
                    Name = "test-network",
                    Mutation = MutationType.Remove,
                }
            ],
            NetworkAdapters =
            [
                new CatletNetworkAdapterConfig
                {
                    Name = "eth0",
                    Mutation = MutationType.Remove,
                }
            ],
            Fodder = 
            [
                new FodderConfig
                {
                    Name = "test-fodder",
                    Remove = true,
                }
            ]
        };

        var result = CatletPedigree.Breed(config, Empty, Empty);

        var resultConfig = result.Should().BeRight().Subject;
        resultConfig.Capabilities.Should().BeEmpty();
        resultConfig.Drives.Should().BeEmpty();
        resultConfig.Networks.Should().BeEmpty();
        resultConfig.NetworkAdapters.Should().BeEmpty();
        resultConfig.Fodder.Should().BeEmpty();
    }
}
