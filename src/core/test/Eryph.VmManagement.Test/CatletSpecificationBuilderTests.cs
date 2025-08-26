using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
    private readonly MockGenePoolReader _genepoolReaderMock = new();

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

        _genepoolReaderMock.SetupGeneSet("acme/acme-os/starter", "acme/acme-os/starter-1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/starter-1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/latest", "acme/acme-os/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-images/latest", "acme/acme-images/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-images/1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/latest", "acme/acme-tools/1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);


        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);
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
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/starter"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/latest"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-images/latest"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-tools/latest"), CancellationToken.None),
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

        _genepoolReaderMock.SetupGeneSet("acme/acme-os/starter-1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-images/1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-tools/1.0", None);


        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);
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

        // Gene sets should only be resolved exactly once.
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/1.0"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-os/1.0"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-images/1.0"), CancellationToken.None),
            Times.Once);
        _genepoolReaderMock.Verify(
            m => m.GetReferencedGeneSet(GeneSetIdentifier.New("acme/acme-tools/1.0"), CancellationToken.None),
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

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);
        
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

        _genepoolReaderMock.SetupGeneSet("acme/acme-os/starter-1.0", None);


        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

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

        
        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

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

        
        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

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

        
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/first", "acme/acme-os/first-1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/first-1.0", None);
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/second", "acme/acme-os/second-1.0");
        _genepoolReaderMock.SetupGeneSet("acme/acme-os/second-1.0", None);

        var either = await CatletSpecificationBuilder.ResolveConfig(config, _genepoolReaderMock.Object, CancellationToken.None);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be(
            "Could not resolve genes in the ancestor catlet "
            + "-> (acme/acme-os/first -> acme/acme-os/first-1.0) "
            + "-> (acme/acme-os/second -> acme/acme-os/second-1.0) "
            + "-> (acme/acme-os/first -> acme/acme-os/first-1.0).");
        error.Inner.Should().BeSome().Which.Message.Should().Be(
            "The pedigree contains a circle.");
    }

    private HashMap<UniqueGeneIdentifier, GeneHash> _genes = HashMap(
        (UniqueGeneIdentifier.New("catlet::gene:acme/acme-os/1.0:catlet[any]"), GeneHash.New(ComputeHash("catlet"))),
        (UniqueGeneIdentifier.New("catlet::gene:acme/embedded-os/1.0:catlet[any]"), GeneHash.New(ComputeHash("catlet-embedded"))),
        (UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:first-food[any]"), GeneHash.New(ComputeHash("first-food-any"))),
        (UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:first-food[hyperv/any]"), GeneHash.New(ComputeHash("first-food-hyperv-any"))),
        (UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:first-food[hyperv/amd64]"), GeneHash.New(ComputeHash("first-food-hyperv-amd64"))),
        (UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:second-food[any]"), GeneHash.New(ComputeHash("second-food-any"))),
        (UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:second-food[hyperv/any]"), GeneHash.New(ComputeHash("second-food-hyperv-any"))),
        (UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:third-food[any]"), GeneHash.New(ComputeHash("third-food-any"))),
        (UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:fourth-food[hyperv/any]"), GeneHash.New(ComputeHash("fourth-food-hyperv-any"))),
        (UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:fifth-food[hyperv/amd64]"), GeneHash.New(ComputeHash("fifth-food-hyperv-amd64"))),
        (UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sda[any]"), GeneHash.New(ComputeHash("sda-any"))),
        (UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sda[hyperv/any]"), GeneHash.New(ComputeHash("sda-hyperv-any"))),
        (UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sda[hyperv/amd64]"), GeneHash.New(ComputeHash("sda-hyperv-amd64"))),
        (UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sdb[any]"), GeneHash.New(ComputeHash("sdb-any"))),
        (UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sdb[hyperv/any]"), GeneHash.New(ComputeHash("sdb-hyperv-any"))),
        (UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sdc[any]"), GeneHash.New(ComputeHash("sdc-any"))),
        (UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sdd[hyperv/any]"), GeneHash.New(ComputeHash("sdd-hyperv-any"))),
        (UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sde[hyperv/amd64]"), GeneHash.New(ComputeHash("sde-hyperv-amd64"))));

    [Theory]
    [InlineData(GeneType.Catlet, "catlet", "any", "catlet")]
    [InlineData(GeneType.Fodder, "first-food", "any", "first-food-any")]
    [InlineData(GeneType.Fodder, "first-food", "hyperv/any", "first-food-hyperv-any")]
    [InlineData(GeneType.Fodder, "first-food", "hyperv/amd64", "first-food-hyperv-amd64")]
    [InlineData(GeneType.Volume, "sda", "any", "sda-any")]
    [InlineData(GeneType.Volume, "sda", "hyperv/any", "sda-hyperv-any")]
    [InlineData(GeneType.Volume, "sda", "hyperv/amd64", "sda-hyperv-amd64")]
    public void ResolveGene_GeneExists_ReturnsGene(
        GeneType geneType, string geneName, string architecture, string expectedContent)
    {
        var geneSetId = GeneSetIdentifier.New("acme/acme-os/1.0");
        var geneId = new GeneIdentifier(geneSetId, GeneName.New(geneName));
        var geneIdAndType = new GeneIdentifierWithType(geneType, geneId);

        var expectedUniqueId = new UniqueGeneIdentifier(geneType, geneId, Architecture.New(architecture));
        var expectedGeneHash = GeneHash.New(ComputeHash(expectedContent));

        var result = CatletSpecificationBuilder.ResolveGene(geneIdAndType, Architecture.New(architecture), _genes);

        var (resolvedId, resolvedHash) = result.Should().BeSuccess().Subject;
        resolvedId.Should().Be(expectedUniqueId);
        resolvedHash.Should().Be(expectedGeneHash);
    }

    [Theory]
    [InlineData(GeneType.Catlet, "acme/acme-os/1.0", "catlet", "any", "catlet")]
    [InlineData(GeneType.Catlet, "acme/embedded-os/1.0", "catlet", "any", "catlet-embedded")]
    public void ResolveGene_GeneExists_ReturnsGeneFromCorrectGeneSet(
        GeneType geneType, string geneSetId, string geneName, string architecture, string expectedContent)
    {
        var geneId = new GeneIdentifier(GeneSetIdentifier.New(geneSetId), GeneName.New(geneName));
        var geneIdAndType = new GeneIdentifierWithType(geneType, geneId);

        var expectedUniqueId = new UniqueGeneIdentifier(geneType, geneId, Architecture.New(architecture));
        var expectedGeneHash = GeneHash.New(ComputeHash(expectedContent));

        var result = CatletSpecificationBuilder.ResolveGene(geneIdAndType, Architecture.New(architecture), _genes);

        var (resolvedId, resolvedHash) = result.Should().BeSuccess().Subject;
        resolvedId.Should().Be(expectedUniqueId);
        resolvedHash.Should().Be(expectedGeneHash);
    }

    [Theory]
    [InlineData(GeneType.Catlet, "catlet", "any", "catlet")]
    [InlineData(GeneType.Fodder, "first-food", "hyperv/amd64", "first-food-hyperv-amd64")]
    [InlineData(GeneType.Fodder, "second-food", "hyperv/any", "second-food-hyperv-any")]
    [InlineData(GeneType.Fodder, "third-food", "any", "third-food-any")]
    [InlineData(GeneType.Volume, "sda", "hyperv/amd64", "sda-hyperv-amd64")]
    [InlineData(GeneType.Volume, "sdb", "hyperv/any", "sdb-hyperv-any")]
    [InlineData(GeneType.Volume, "sdc", "any", "sdc-any")]
    public void ResolveGene_UsableGeneExists_ReturnsArchitecture(
        GeneType geneType, string geneName, string expectedArchitecture, string expectedContent)
    {
        var geneSetId = GeneSetIdentifier.New("acme/acme-os/1.0");
        var geneId = new GeneIdentifier(geneSetId, GeneName.New(geneName));
        var geneIdAndType = new GeneIdentifierWithType(geneType, geneId);
        var architecture = Architecture.New("hyperv/amd64");

        var expectedUniqueId = new UniqueGeneIdentifier(geneType, geneId, Architecture.New(expectedArchitecture));
        var expectedGeneHash = GeneHash.New(ComputeHash(expectedContent));

        var result = CatletSpecificationBuilder.ResolveGene(geneIdAndType, architecture, _genes);

        var (resolvedId, resolvedHash) = result.Should().BeSuccess().Subject;
        resolvedId.Should().Be(expectedUniqueId);
        resolvedHash.Should().Be(expectedGeneHash);
    }

    [Theory]
    [InlineData(GeneType.Fodder, "invalid-food", "any")]
    [InlineData(GeneType.Volume, "sdz", "any")]
    public void ResolveGene_GeneDoesNotExist_ReturnsError(
        GeneType geneType, string geneName, string architecture)
    {
        var geneSetId = GeneSetIdentifier.New("acme/acme-os/1.0");
        var geneId = new GeneIdentifier(geneSetId, GeneName.New(geneName));
        var geneIdAndType = new GeneIdentifierWithType(geneType, geneId);

        var result = CatletSpecificationBuilder.ResolveGene(geneIdAndType, Architecture.New(architecture), _genes);

        result.Should().BeFail().Which
            .Should().ContainSingle().Which.Message
            .Should().Be($"The gene {geneType.ToString().ToLowerInvariant()}::{geneId} does not exist.");
    }

    [Theory]
    [InlineData(GeneType.Fodder, "fourth-food", "any")]
    [InlineData(GeneType.Fodder, "fifth-food", "any")]
    [InlineData(GeneType.Volume, "sdd", "any")]
    [InlineData(GeneType.Volume, "sde", "any")]
    public void ResolveGene_GeneIsNotCompatibleWithHypervisor_ReturnsError(
        GeneType geneType, string geneName, string architecture)
    {
        var geneSetId = GeneSetIdentifier.New("acme/acme-os/1.0");
        var geneId = new GeneIdentifier(geneSetId, GeneName.New(geneName));
        var geneIdAndType = new GeneIdentifierWithType(geneType, geneId);

        var result = CatletSpecificationBuilder.ResolveGene(geneIdAndType, Architecture.New(architecture), _genes);

        result.Should().BeFail().Which
            .Should().ContainSingle().Which.Message
            .Should().Match($"The gene {geneType.ToString().ToLowerInvariant()}::{geneId} is not compatible with the hypervisor {Architecture.New(architecture).Hypervisor}.");
    }

    [Theory]
    [InlineData(GeneType.Fodder, "fifth-food", "hyperv/any")]
    [InlineData(GeneType.Volume, "sde", "hyperv/any")]
    public void ResolveGene_GeneIsNotCompatibleWithProcessorArchitecture_ReturnsError(
        GeneType geneType, string geneName, string architecture)
    {
        var geneSetId = GeneSetIdentifier.New("acme/acme-os/1.0");
        var geneId = new GeneIdentifier(geneSetId, GeneName.New(geneName));
        var geneIdAndType = new GeneIdentifierWithType(geneType, geneId);

        var result = CatletSpecificationBuilder.ResolveGene(geneIdAndType, Architecture.New(architecture), _genes);

        result.Should().BeFail().Which
            .Should().ContainSingle().Which.Message
            .Should().Match($"The gene {geneType.ToString().ToLowerInvariant()}::{geneId} is not compatible with the processor architecture {Architecture.New(architecture).ProcessorArchitecture}.");
    }

    private static string ComputeHash(string value) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";
}
