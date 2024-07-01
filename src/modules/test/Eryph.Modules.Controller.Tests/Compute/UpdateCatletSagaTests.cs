using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.GenePool.Model;
using Eryph.Modules.Controller.Compute;

namespace Eryph.Modules.Controller.Tests.Compute;

public class UpdateCatletSagaTests
{
    [Fact]
    public void FindRequiredGenes_InformationalParentSource_ReturnsGenesWithInformationalParents()
    {
        var config = new CatletConfig()
        {
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:dbosoft/test/1.0:test-fodder"
                },
                new FodderConfig()
                {
                    Source = "gene:dbosoft/test/1.0:catlet"
                }
            ]
        };

        var result = UpdateCatletSaga.FindRequiredGenes(config);

        result.Should().BeSuccess().Which.Should().SatisfyRespectively(
            geneId =>
            {
                geneId.GeneIdentifier.Should().Be(GeneIdentifier.New("gene:dbosoft/test/1.0:test-fodder"));
                geneId.GeneType.Should().Be(GeneType.Fodder);
            });
    }
}
