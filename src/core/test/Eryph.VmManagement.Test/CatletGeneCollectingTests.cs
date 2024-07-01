using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.GenePool.Model;
using Eryph.Genetics;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt.Common;
using Xunit;

namespace Eryph.VmManagement.Test;

public class CatletGeneCollectingTests
{
    [Fact]
    public void CreatePrepareGeneCommands_ValidSources_ReturnsCommands()
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
    public void CreatePrepareGeneCommands_InvalidSources_ReturnsFail()
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
