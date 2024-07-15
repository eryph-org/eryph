using System;
using System.Collections.Generic;
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

    public static readonly IEnumerable<GeneSetIdentifier> ParentSources =
    [
        ParentId, ParentRefId,
    ];

    [Theory, CombinatorialData]
    public void Breed_ChildUsesDriveGeneFromParent_ResolvedDriveSourceIsIncluded(
        [CombinatorialMemberData(nameof(ParentSources))] GeneSetIdentifier parentId)
    {
        var parentConfig = new CatletConfig
        {
            Name = "parent",
            Drives =
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                }
            ]
        };

        var config = new CatletConfig
        {
            Name = "child",
            Parent = parentId.Value,
        };

        var geneSetMap = HashMap((parentId, ParentId));
        var ancestors = HashMap((ParentId, parentConfig));

        var result = CatletPedigree.Breed(config, geneSetMap, ancestors);

        result.Should().BeRight().Which.Config.Drives.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:acme/acme-parent/1.0:sda"));
    }

    [Theory, CombinatorialData]
    public void Breed_ChildUsesFodderFromParent_ResolvedFodderSourceIsIncluded(
        [CombinatorialMemberData(nameof(ParentSources))] GeneSetIdentifier parentId)
    {
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

        var config = new CatletConfig
        {
            Name = "test",
            Parent = parentId.Value,
        };

        var geneSetMap = HashMap((parentId, ParentId));
        var ancestors = HashMap((ParentId, parentConfig));

        var result = CatletPedigree.Breed(config, geneSetMap, ancestors);

        result.Should().BeRight().Which.Config.Fodder.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:acme/acme-parent/1.0:catlet"));
    }

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
}
