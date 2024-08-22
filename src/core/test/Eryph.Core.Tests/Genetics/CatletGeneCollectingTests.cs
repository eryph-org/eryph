using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using LanguageExt.Common;

namespace Eryph.Core.Tests.Genetics;

public class CatletGeneCollectingTests
{
    [Fact]
    public void CollectGenes_ValidSources_ReturnsGeneIdentifiers()
    {
        var config = new CatletConfig()
        {
            Drives =
            [
                new CatletDriveConfig()
                {
                    Source = "gene:dbosoft/test/1.0:sda"
                },
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:dbosoft/test/1.0:test-fodder"
                },
                new FodderConfig()
                {
                    Source = "gene:dbosoft/test/1.0:test-fodder"
                },
                new FodderConfig()
                {
                    Source = "gene:dbosoft/test/1.0:catlet"
                },
            ]
        };

        var result = CatletGeneCollecting.CollectGenes(config);

        result.Should().BeSuccess().Which.Should().SatisfyRespectively(
            geneId =>
            {
                geneId.GeneIdentifier.Should().Be(GeneIdentifier.New("gene:dbosoft/test/1.0:sda"));
                geneId.GeneType.Should().Be(GeneType.Volume);
            },
            geneId =>
            {
                geneId.GeneIdentifier.Should().Be(GeneIdentifier.New("gene:dbosoft/test/1.0:test-fodder"));
                geneId.GeneType.Should().Be(GeneType.Fodder);
            },
            geneId =>
            {
                geneId.GeneIdentifier.Should().Be(GeneIdentifier.New("gene:dbosoft/test/1.0:catlet"));
                geneId.GeneType.Should().Be(GeneType.Fodder);
            });
    }

    [Fact]
    public void CollectGenes_DriveSourceIsAPath_IgnoresDriveSource()
    {
        var config = new CatletConfig()
        {
            Drives =
            [
                new CatletDriveConfig()
                {
                    Source = @"Z:\test\test.vhdx"
                },
            ],
        };

        var result = CatletGeneCollecting.CollectGenes(config);

        result.Should().BeSuccess().Which.Should().BeEmpty();
    }

    [Fact]
    public void CollectGenes_InvalidSources_ReturnsFail()
    {
        var config = new CatletConfig()
        {
            Drives =
            [
                new CatletDriveConfig()
                {
                    Source = "gene:invalid|geneset:invalid|drive|gene"
                },
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:invalid|geneset:invalid|fodder|gene"
                },
            ]
        };

        var result = CatletGeneCollecting.CollectGenes(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            error =>
            {
                error.Message.Should().Be("The gene source 'gene:invalid|geneset:invalid|drive|gene' is invalid.");
                error.Inner.Should().BeSome()
                    .Which.Should().BeOfType<Expected>()
                    .Which.Message.Should().Be(
                        "The gene set identifier is malformed. It must be either org/geneset or org/geneset/tag.");
            },
            error =>
            {
                error.Message.Should().Be("The gene source 'gene:invalid|geneset:invalid|fodder|gene' is invalid.");
                error.Inner.Should().BeSome()
                    .Which.Should().BeOfType<Expected>()
                    .Which.Message.Should().Be(
                        "The gene set identifier is malformed. It must be either org/geneset or org/geneset/tag.");
            });
    }
}
