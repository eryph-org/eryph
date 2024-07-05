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

        result.Should().BeLeft().Which.Message.Should().Be("Circle detected");
    }
}