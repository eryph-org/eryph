using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using Eryph.VmManagement.TestBase;
using Moq;

namespace Eryph.Modules.VmHostAgent.Test;

public class ResolveCatletConfigCommandHandlerTests
{
    private readonly Mock<ILocalGenepoolReader> _genepoolReaderMock = new();
    private readonly Mock<IGeneProvider> _geneProviderMock = new();

    [Fact]
    public async Task Handle_ComplexConfig_ReturnsResolvedData()
    {
        var cancelToken = new CancellationToken();

        var config = new CatletConfig
        {
            Parent = "acme/acme-os/starter",
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = "gene:acme/acme-images:first-image",
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools:test-fodder",
                }
            ],
        };

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/starter-1.0",
            new CatletConfig()
            {
                Name = "acme-os-starter",
                Parent = "acme/acme-os/latest",
                Fodder = 
                [
                    new FodderConfig()
                    {
                        Source = "gene:acme/acme-tools:other-test-fodder",
                    }
                ]
            });

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/1.0",
            new CatletConfig()
            {
                Name = "acme-os-base",
            });

        _geneProviderMock.SetupGeneSets(
            ("acme/acme-os/starter", "acme/acme-os/starter-1.0"),
            ("acme/acme-os/latest", "acme/acme-os/1.0"),
            ("acme/acme-images/latest", "acme/acme-images/1.0"),
            ("acme/acme-tools/latest", "acme/acme-tools/1.0"));

        _geneProviderMock.SetupGenes(
            (GeneType.Catlet, "acme/acme-os/starter-1.0"),
            (GeneType.Catlet, "acme/acme-os/1.0"));

        var result = await ResolveCatletConfigCommandHandler.Handle(
            new ResolveCatletConfigCommand { Config = config },
            _geneProviderMock.Object,
            _genepoolReaderMock.Object);

        var commandResponse = result.Should().BeRight().Subject;
        commandResponse.ResolvedGeneSets.Should().BeEquivalentTo([
            (GeneSetIdentifier.New("acme/acme-os/latest"), GeneSetIdentifier.New("acme/acme-os/1.0")),
            (GeneSetIdentifier.New("acme/acme-os/1.0"), GeneSetIdentifier.New("acme/acme-os/1.0")),
            (GeneSetIdentifier.New("acme/acme-images/latest"), GeneSetIdentifier.New("acme/acme-images/1.0")),
            (GeneSetIdentifier.New("acme/acme-images/1.0"), GeneSetIdentifier.New("acme/acme-images/1.0")),
            (GeneSetIdentifier.New("acme/acme-tools/latest"), GeneSetIdentifier.New("acme/acme-tools/1.0")),
            (GeneSetIdentifier.New("acme/acme-tools/1.0"), GeneSetIdentifier.New("acme/acme-tools/1.0"))
        ]);
        commandResponse.ParentConfigs.Should().SatisfyRespectively(
            ancestor =>
            {
                ancestor.Id.Should().Be(GeneSetIdentifier.New("acme/acme-os/starter-1.0"));
                ancestor.Config.Name.Should().Be("acme-os-starter");
            },
            ancestor =>
            {
                ancestor.Id.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
                ancestor.Config.Name.Should().Be("acme-os-base");
            });

        // Gene sets should only be resolved exactly once.
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-os/starter"), cancelToken),
            Times.Once);
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-os/latest"), cancelToken),
            Times.Once);
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-images/latest"), cancelToken),
            Times.Once);
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-tools/latest"), cancelToken),
            Times.Once);
    }
}
