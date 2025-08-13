using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using Eryph.GenePool;
using Eryph.VmManagement.TestBase;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;


public class CatletSpecificationBuilderTests
{
    private readonly Mock<IGenePoolReader> _genepoolReaderMock = new();
    private readonly CancellationToken _cancelToken = new();

    [Fact]
    public async Task ResolveConfig_ConfigWithGeneSetTagReferences_ReturnsResolvedData()
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

        _genepoolReaderMock.SetupGeneSets(
            ("acme/acme-os/starter", "acme/acme-os/starter-1.0"),
            ("acme/acme-os/starter-1.0", null),
            ("acme/acme-os/latest", "acme/acme-os/1.0"),
            ("acme/acme-os/1.0", null),
            ("acme/acme-images/latest", "acme/acme-images/1.0"),
            ("acme/acme-images/1.0", null),
            ("acme/acme-tools/latest", "acme/acme-tools/1.0"),
            ("acme/acme-tools/1.0", null));

        /*
        _geneProviderMock.SetupGenes(
            (GeneType.Catlet, "gene:acme/acme-os/starter-1.0:catlet", "any"),
            (GeneType.Catlet, "gene:acme/acme-os/1.0:catlet", "any"));


        ArrangeInventory("acme/acme-os/starter-1.0", "acme/acme-os/1.0");
        */

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, _cancelToken);
        var result = either.Should().BeRight().Subject;

        var resolvedGeneSets = result.ResolvedGeneSets.ToDictionary();
        var resolvedParents = result.ResolvedCatlets.ToDictionary();


        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/starter-1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/latest"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-images/latest"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-images/1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-tools/latest"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-tools/1.0"));

        resolvedParents.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0"))
            .WhoseValue.Name.Should().Be("acme-os-base");
        resolvedParents.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter-1.0"))
            .WhoseValue.Name.Should().Be("acme-os-starter");

        /*
        commandResponse.Inventory.Should().Satisfy(
            geneData => geneData.Id.GeneType == GeneType.Catlet
                        && geneData.Id.Id.GeneSet == GeneSetIdentifier.New("acme/acme-os/starter-1.0")
                        && geneData.Id.Id.GeneName == GeneName.New("catlet"),
            geneData => geneData.Id.GeneType == GeneType.Catlet
                        && geneData.Id.Id.GeneSet == GeneSetIdentifier.New("acme/acme-os/1.0")
                        && geneData.Id.Id.GeneName == GeneName.New("catlet"));
        */

        // Gene sets should only be resolved exactly once.
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/starter"), _cancelToken),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/latest"), _cancelToken),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-images/latest"), _cancelToken),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-tools/latest"), _cancelToken),
            Times.Once);
    }

    [Fact]
    public async Task Resolve_ConfigWithResolvedGeneSetTags_ReturnsResolvedData()
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

        _genepoolReaderMock.SetupGeneSets(
            ("acme/acme-os/starter-1.0", null),
            ("acme/acme-os/1.0", null),
            ("acme/acme-images/1.0", null),
            ("acme/acme-tools/1.0", null));


        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, _cancelToken);
        var result = either.Should().BeRight().Subject;

        var resolvedGeneSets = result.ResolvedGeneSets.ToDictionary();
        var resolvedParents = result.ResolvedCatlets.ToDictionary();

        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter-1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/starter-1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-images/1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-images/1.0"));
        resolvedGeneSets.Should().ContainKey(GeneSetIdentifier.New("acme/acme-tools/1.0"))
            .WhoseValue.Should().Be(GeneSetIdentifier.New("acme/acme-tools/1.0"));

        resolvedParents.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/1.0"))
            .WhoseValue.Name.Should().Be("acme-os-base");
        resolvedParents.Should().ContainKey(GeneSetIdentifier.New("acme/acme-os/starter-1.0"))
            .WhoseValue.Name.Should().Be("acme-os-starter");

        /*
        commandResponse.Inventory.Should().Satisfy(
            geneData => geneData.Id.GeneType == GeneType.Catlet
                        && geneData.Id.Id.GeneSet == GeneSetIdentifier.New("acme/acme-os/starter-1.0")
                        && geneData.Id.Id.GeneName == GeneName.New("catlet"),
            geneData => geneData.Id.GeneType == GeneType.Catlet
                        && geneData.Id.Id.GeneSet == GeneSetIdentifier.New("acme/acme-os/1.0")
                        && geneData.Id.Id.GeneName == GeneName.New("catlet"));
        */

        // Gene sets should only be resolved exactly once.
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/1.0"), _cancelToken),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/1.0"), _cancelToken),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-images/1.0"), _cancelToken),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-tools/1.0"), _cancelToken),
            Times.Once);
    }

    [Fact]
    public async Task ResolveConfig_DriveSourceIsAPath_IgnoresDriveSource()
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

        _genepoolReaderMock.SetupGeneSets();
        

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, _cancelToken);
        
        var result = either.Should().BeRight().Subject;
        result.ResolvedCatlets.Should().BeEmpty();
        result.ResolvedGeneSets.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveConfig_MissingGrandParent_ReturnsError()
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

        _genepoolReaderMock.SetupGeneSets(
            ("acme/acme-os/starter-1.0", null));


        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, _cancelToken);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve genes in the ancestor catlet -> acme/acme-os/starter-1.0.");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "Could not resolve the gene set tag 'acme/acme-os/1.0'.");
    }

    [Fact]
    public async Task ResolveConfig_MissingDriveSource_ReturnsError()
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

        _genepoolReaderMock.SetupGeneSets();

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, _cancelToken);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve genes in the catlet config.");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "Could not resolve the gene set tag 'acme/acme-images/1.0'.");
    }

    [Fact]
    public async Task ResolveConfig_MissingFodderSource_ReturnsError()
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

        _genepoolReaderMock.SetupGeneSets();

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, _cancelToken);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve genes in the catlet config.");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "Could not resolve the gene set tag 'acme/acme-tools/1.0'.");
    }

    [Fact]
    public async Task ResolveConfig_AncestorsHaveCircle_ReturnsError()
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

        _genepoolReaderMock.SetupGeneSets(
            ("acme/acme-os/first", "acme/acme-os/first-1.0"),
            ("acme/acme-os/first-1.0", null),
            ("acme/acme-os/second", "acme/acme-os/second-1.0"),
            ("acme/acme-os/second-1.0", null));

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, _cancelToken);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be(
            "Could not resolve genes in the ancestor catlet "
            + "-> (acme/acme-os/first -> acme/acme-os/first-1.0) "
            + "-> (acme/acme-os/second -> acme/acme-os/second-1.0) "
            + "-> (acme/acme-os/first -> acme/acme-os/first-1.0).");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "The pedigree contains a circle.");
    }
}
