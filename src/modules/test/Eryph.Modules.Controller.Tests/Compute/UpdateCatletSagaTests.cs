using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.GenePool.Model;
using Eryph.Modules.Controller.Compute;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Tests.Compute;

public class UpdateCatletSagaTests
{
    private const string AgentName = "test-agent";

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
            ]
        };

        var result = UpdateCatletSaga.CreatePrepareGeneCommands(config);

        result.Should().BeSuccess().Which.Should().SatisfyRespectively(
            command =>
            {
                command.GeneIdentifier.Should().Be(GeneIdentifier.New("gene:dbosoft/test/1.0:sda"));
                command.GeneType.Should().Be(GeneType.Volume);
            },
            command =>
            {
                command.GeneIdentifier.Should().Be(GeneIdentifier.New("gene:dbosoft/test/1.0:test-fodder"));
                command.GeneType.Should().Be(GeneType.Fodder);
            });
    }

    [Fact]
    public void CreatePrepareGeneCommands_InformationalParentSource_ReturnsNoCommand()
    {
        var config = new CatletConfig()
        {
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:dbosoft/test/1.0:catlet"
                }
            ]
        };

        var result = UpdateCatletSaga.CreatePrepareGeneCommands(config);

        result.Should().BeSuccess().Which.Should().BeEmpty();
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

        var result = UpdateCatletSaga.CreatePrepareGeneCommands(config);

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
