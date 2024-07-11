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
    private readonly GeneSetIdentifier _parentId = new("acme/acme-parent/1.0");
    private readonly GeneSetIdentifier _parentRefId = new("acme/acme-parent/latest");
    private readonly GeneSetIdentifier _grandParentId = new("acme/acme-grand-parent/1.0");
    private readonly GeneSetIdentifier _grandParentRefId = new("acme/acme-grand-parent/1.0");

    [Fact]
    public void Breed_ChildUsesDriveGeneFromParent_ResolvedDriveSourceIsIncluded()
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
            Parent = _parentId.Value,
        };

        var geneSetMap = HashMap((_parentId, _parentId));
        var ancestors = HashMap((_parentId, parentConfig));

        var result = CatletPedigree.Breed(config, geneSetMap, ancestors);

        result.Should().BeRight().Which.Config.Drives.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:acme/acme-parent/1.0:sda"));
    }

    [Fact]
    public void Breed_ChildUsesFodderFromParent_ResolvedFodderSourceIsIncluded()
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
            Parent = _parentId.Value,
        };

        var geneSetMap = HashMap((_parentId, _parentId));
        var ancestors = HashMap((_parentId, parentConfig));

        var result = CatletPedigree.Breed(config, geneSetMap, ancestors);

        result.Should().BeRight().Which.Config.Fodder.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:acme/acme-parent/1.0:catlet"));
    }

    [Fact]
    public void Breed_PedigreeContainsCircle_ReturnsFail()
    {
        var parentConfig = new CatletConfig
        {
            Parent = _parentId.Value,
        };

        var grandParentConfig = new CatletConfig
        {
            Parent = _grandParentId.Value,
        };

        var geneSetMap = HashMap((_parentId, _parentId), (_grandParentId, _grandParentId));
        var ancestors = HashMap((_parentId, parentConfig), (_grandParentId, grandParentConfig));

        var config = new CatletConfig
        {
            Parent = _parentId.Value,
        };

        var result = CatletPedigree.Breed(config, geneSetMap, ancestors);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be(
            "Could not breed ancestor in the pedigree catlet -> acme/acme-parent/1.0 -> acme/acme-parent/1.0.");
        error.Inner.Should().BeSome().Which.Message
            .Should().Be("The pedigree contains a circle.");
    }
}
