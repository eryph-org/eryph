using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.GenePool;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.VmManagement;
using Eryph.VmManagement.TestBase;
using LanguageExt;
using LanguageExt.Common;
using Moq;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test;

public class ResolveCatletConfigCommandHandlerTests
{
    private readonly Mock<IGenePoolInventory> _genePoolInventoryMock = new();
    private readonly Mock<ILocalGenepoolReader> _genepoolReaderMock = new();
    private readonly Mock<IGeneProvider> _geneProviderMock = new();
    private readonly CancellationToken _cancelToken = new();

    [Fact]
    public async Task Handle_ConfigWithGeneSetTagReferences_ReturnsResolvedData()
    {
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
            (GeneType.Catlet, "gene:acme/acme-os/starter-1.0:catlet"),
            (GeneType.Catlet, "gene:acme/acme-os/1.0:catlet"));

        ArrangeInventory("acme/acme-os/starter-1.0", "acme/acme-os/1.0");

        var result = await ResolveCatletConfigCommandHandler.Handle(
            new ResolveCatletConfigCommand { Config = config },
            _geneProviderMock.Object,
            _genepoolReaderMock.Object,
            _genePoolInventoryMock.Object,
            _cancelToken);

        var commandResponse = result.Should().BeRight().Subject;

        commandResponse.ResolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/starter-1.0"));
        commandResponse.ResolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/latest"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
        commandResponse.ResolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-images/latest"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-images/1.0"));
        commandResponse.ResolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-tools/latest"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-tools/1.0"));
        
        commandResponse.ParentConfigs.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0"))
            .WhoseValue.Name.Should().Be("acme-os-base");
        commandResponse.ParentConfigs.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter-1.0"))
            .WhoseValue.Name.Should().Be("acme-os-starter");

        commandResponse.Inventory.Should().SatisfyRespectively(
            geneData =>
            {
                geneData.GeneType.Should().Be(GeneType.Catlet);
                geneData.Id.GeneSet.Should().Be(GeneSetIdentifier.New("acme/acme-os/starter-1.0"));
                geneData.Id.GeneName.Should().Be(GeneName.New("catlet"));
            },
            geneData =>
            {
                geneData.GeneType.Should().Be(GeneType.Catlet);
                geneData.Id.GeneSet.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
                geneData.Id.GeneName.Should().Be(GeneName.New("catlet"));
            });

        // Gene sets should only be resolved exactly once.
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-os/starter"), _cancelToken),
            Times.Once);
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-os/latest"), _cancelToken),
            Times.Once);
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-images/latest"), _cancelToken),
            Times.Once);
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-tools/latest"), _cancelToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ConfigWithResolvedGeneSetTags_ReturnsResolvedData()
    {
        var config = new CatletConfig
        {
            Parent = "acme/acme-os/starter-1.0",
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = "gene:acme/acme-images/1.0:first-image",
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                }
            ],
        };

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/starter-1.0",
            new CatletConfig()
            {
                Name = "acme-os-starter",
                Parent = "acme/acme-os/1.0",
                Fodder =
                [
                    new FodderConfig()
                    {
                        Source = "gene:acme/acme-tools/1.0:other-test-fodder",
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
            ("acme/acme-os/starter-1.0", "acme/acme-os/starter-1.0"),
            ("acme/acme-os/1.0", "acme/acme-os/1.0"),
            ("acme/acme-images/1.0", "acme/acme-images/1.0"),
            ("acme/acme-tools/1.0", "acme/acme-tools/1.0"));

        _geneProviderMock.SetupGenes(
            (GeneType.Catlet, "gene:acme/acme-os/starter-1.0:catlet"),
            (GeneType.Catlet, "gene:acme/acme-os/1.0:catlet"));

        ArrangeInventory("acme/acme-os/starter-1.0", "acme/acme-os/1.0");

        var result = await ResolveCatletConfigCommandHandler.Handle(
            new ResolveCatletConfigCommand { Config = config },
            _geneProviderMock.Object,
            _genepoolReaderMock.Object,
            _genePoolInventoryMock.Object,
            _cancelToken);

        var commandResponse = result.Should().BeRight().Subject;

        commandResponse.ResolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter-1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/starter-1.0"));
        commandResponse.ResolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
        commandResponse.ResolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-images/1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-images/1.0"));
        commandResponse.ResolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-tools/1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-tools/1.0"));

        commandResponse.ParentConfigs.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0"))
            .WhoseValue.Name.Should().Be("acme-os-base");
        commandResponse.ParentConfigs.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter-1.0"))
            .WhoseValue.Name.Should().Be("acme-os-starter");

        commandResponse.Inventory.Should().SatisfyRespectively(
            geneData =>
            {
                geneData.GeneType.Should().Be(GeneType.Catlet);
                geneData.Id.GeneSet.Should().Be(GeneSetIdentifier.New("acme/acme-os/starter-1.0"));
                geneData.Id.GeneName.Should().Be(GeneName.New("catlet"));
            },
            geneData =>
            {
                geneData.GeneType.Should().Be(GeneType.Catlet);
                geneData.Id.GeneSet.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
                geneData.Id.GeneName.Should().Be(GeneName.New("catlet"));
            });

        // Gene sets should only be resolved exactly once.
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-os/1.0"), _cancelToken),
            Times.Once);
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-os/1.0"), _cancelToken),
            Times.Once);
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-images/1.0"), _cancelToken),
            Times.Once);
        _geneProviderMock.Verify(
            m => m.ResolveGeneSet(GeneSetIdentifier.New("acme/acme-tools/1.0"), _cancelToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DriveSourceIsAPath_IgnoresDriveSource()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = @"Z:\test\test.vhdx",
                }
            ],
        };

        _geneProviderMock.SetupGeneSets();
        _geneProviderMock.SetupGenes();
        ArrangeInventory();

        var result = await ResolveCatletConfigCommandHandler.Handle(
            new ResolveCatletConfigCommand { Config = config },
            _geneProviderMock.Object,
            _genepoolReaderMock.Object,
            _genePoolInventoryMock.Object,
            _cancelToken);

        var commandResponse = result.Should().BeRight().Subject;
        commandResponse.ParentConfigs.Should().BeEmpty();
        commandResponse.ResolvedGeneSets.Should().BeEmpty();
        commandResponse.Inventory.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MissingGrandParent_ReturnsError()
    {
        var config = new CatletConfig
        {
            Parent = "acme/acme-os/starter-1.0",
        };

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/starter-1.0",
            new CatletConfig()
            {
                Name = "acme-os-starter",
                Parent = "acme/acme-os/1.0",
            });

        _geneProviderMock.SetupGeneSets(
            ("acme/acme-os/starter-1.0", "acme/acme-os/starter-1.0"));

        _geneProviderMock.SetupGenes(
            (GeneType.Catlet, "gene:acme/acme-os/starter-1.0:catlet"));

        var result = await ResolveCatletConfigCommandHandler.Handle(
            new ResolveCatletConfigCommand { Config = config },
            _geneProviderMock.Object,
            _genepoolReaderMock.Object,
            _genePoolInventoryMock.Object,
            _cancelToken);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve genes in the ancestor catlet -> acme/acme-os/starter-1.0.");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "Could not resolve the gene set tag 'acme/acme-os/1.0'.");
    }

    [Fact]
    public async Task Handle_MissingDriveSource_ReturnsError()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = "gene:acme/acme-images/1.0:first-image",
                }
            ],
        };

        _geneProviderMock.SetupGeneSets();

        _geneProviderMock.SetupGenes();

        ArrangeInventory();

        var result = await ResolveCatletConfigCommandHandler.Handle(
            new ResolveCatletConfigCommand { Config = config },
            _geneProviderMock.Object,
            _genepoolReaderMock.Object,
            _genePoolInventoryMock.Object,
            _cancelToken);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve genes in the catlet config.");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "Could not resolve the gene set tag 'acme/acme-images/1.0'.");
    }

    [Fact]
    public async Task Handle_MissingFodderSource_ReturnsError()
    {
        var config = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/1.0:test-fodder",
                }
            ],
        };

        _geneProviderMock.SetupGeneSets();

        _geneProviderMock.SetupGenes();

        ArrangeInventory();

        var result = await ResolveCatletConfigCommandHandler.Handle(
            new ResolveCatletConfigCommand { Config = config },
            _geneProviderMock.Object,
            _genepoolReaderMock.Object,
            _genePoolInventoryMock.Object,
            _cancelToken);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve genes in the catlet config.");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "Could not resolve the gene set tag 'acme/acme-tools/1.0'.");
    }

    [Fact]
    public async Task Handle_AncestorsHaveCircle_ReturnsError()
    {
        var config = new CatletConfig
        {
            Parent = "acme/acme-os/first",
        };

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/first-1.0",
            new CatletConfig()
            {
                Name = "acme-os-first-1.0",
                Parent = "acme/acme-os/second",
            });

        _genepoolReaderMock.SetupCatletGene(
            "acme/acme-os/second-1.0",
            new CatletConfig()
            {
                Name = "acme-os-second-1.0",
                Parent = "acme/acme-os/first",
            });

        _geneProviderMock.SetupGeneSets(
            ("acme/acme-os/first", "acme/acme-os/first-1.0"),
            ("acme/acme-os/second", "acme/acme-os/second-1.0"));

        _geneProviderMock.SetupGenes(
            (GeneType.Catlet, "gene:acme/acme-os/first-1.0:catlet"),
            (GeneType.Catlet, "gene:acme/acme-os/second-1.0:catlet"));

        ArrangeInventory("acme/acme-os/first-1.0", "acme/acme-os/second-1.0");

        var result = await ResolveCatletConfigCommandHandler.Handle(
            new ResolveCatletConfigCommand { Config = config },
            _geneProviderMock.Object,
            _genepoolReaderMock.Object,
            _genePoolInventoryMock.Object,
            _cancelToken);

        var error = result.Should().BeLeft().Subject;
        error.Message.Should().Be(
            "Could not resolve genes in the ancestor catlet "
            + "-> (acme/acme-os/first -> acme/acme-os/first-1.0) "
            + "-> (acme/acme-os/second -> acme/acme-os/second-1.0) "
            + "-> (acme/acme-os/first -> acme/acme-os/first-1.0).");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "The pedigree contains a circle.");
    }

    private void ArrangeInventory(params string[] geneSetIds)
    {
        var map = toHashSet(geneSetIds.Map(i => GeneSetIdentifier.New(i)));
        _genePoolInventoryMock.Setup(m => m.InventorizeGeneSet(It.IsAny<GeneSetIdentifier>()))
            .Returns((GeneSetIdentifier geneSetId) => map.Contains(geneSetId)
                ? RightAsync<Error, Seq<GeneData>>(Seq1(new GeneData
                {
                    GeneType = GeneType.Catlet,
                    Id = new GeneIdentifier(geneSetId, GeneName.New("catlet")),
                    Hash = "12345678",
                    Size = 42,
                }))
                : LeftAsync<Error, Seq<GeneData>>(Error.New("The gene set does not exist.")));
    }
}
