using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using Eryph.Modules.Genepool.Genetics;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.GenepoolModule.Test.Genetics;

public class GeneSetTagManifestUtilsTests
{
    private readonly GenesetTagManifestData _manifest = new()
    {
        CatletGene = "hash-catlet",
        FodderGenes =
        [
            new GeneReferenceData()
            {
                Name = "first-food",
                Architecture = "any",
                Hash = "hash-first-food-any"
            },
            new GeneReferenceData()
            {
                Name = "first-food",
                Architecture = "hyperv/any",
                Hash = "hash-first-food-hyperv-any"
            },
            new GeneReferenceData()
            {
                Name = "first-food",
                Architecture = "hyperv/amd64",
                Hash = "hash-first-food-hyperv-amd64"
            },
            new GeneReferenceData()
            {
                Name = "second-food",
                Architecture = "any",
                Hash = "hash-second-food-any"
            },
            new GeneReferenceData()
            {
                Name = "second-food",
                Architecture = "hyperv/any",
                Hash = "hash-second-food-hyperv-any"
            },
            new GeneReferenceData()
            {
                Name = "third-food",
                Architecture = "any",
                Hash = "hash-third-food-any"
            },
        ],
        VolumeGenes =
        [
            new GeneReferenceData()
            {
                Name = "sda",
                Architecture = "any",
                Hash = "hash-sda-any"
            },
            new GeneReferenceData()
            {
                Name = "sda",
                Architecture = "hyperv/any",
                Hash = "hash-sda-hyperv-any"
            },
            new GeneReferenceData()
            {
                Name = "sda",
                Architecture = "hyperv/amd64",
                Hash = "hash-sda-hyperv-amd64"
            },
            new GeneReferenceData()
            {
                Name = "sdb",
                Architecture = "any",
                Hash = "hash-sdb-any"
            },
            new GeneReferenceData()
            {
                Name = "sdb",
                Architecture = "hyperv/any",
                Hash = "hash-sdb-hyperv-any"
            },
            new GeneReferenceData()
            {
                Name = "sdc",
                Architecture = "any",
                Hash = "hash-sdc-any"
            }
        ],
    };

    [Theory]
    [InlineData(GeneType.Catlet, "catlet", "any", "hash-catlet")]
    [InlineData(GeneType.Fodder, "first-food", "any", "hash-first-food-any")]
    [InlineData(GeneType.Fodder, "first-food", "hyperv/any", "hash-first-food-hyperv-any")]
    [InlineData(GeneType.Fodder, "first-food", "hyperv/amd64", "hash-first-food-hyperv-amd64")]
    [InlineData(GeneType.Volume, "sda", "any", "hash-sda-any")]
    [InlineData(GeneType.Volume, "sda", "hyperv/any", "hash-sda-hyperv-any")]
    [InlineData(GeneType.Volume, "sda", "hyperv/amd64", "hash-sda-hyperv-amd64")]
    public void FindGeneHash_GeneExists_ReturnsHash(
        GeneType geneType, string geneName, string architecture, string expectedHash)
    {
        var result = GeneSetTagManifestUtils.FindGeneHash(
            _manifest, geneType, GeneName.New(geneName), Architecture.New(architecture));

        result.Should().BeSome().Which.Should().Be(expectedHash);
    }

    [Theory]
    [InlineData(GeneType.Catlet, "catlet", "hyperv/any")]
    [InlineData(GeneType.Catlet, "catlet", "hyperv/amd64")]
    [InlineData(GeneType.Fodder, "invalid-food", "any")]
    [InlineData(GeneType.Volume, "sdz", "any")]
    public void FindGeneHash_GeneDoesNotExist_ReturnsNone(
        GeneType geneType, string geneName, string architecture)
    {
        var hash = GeneSetTagManifestUtils.FindGeneHash(
            _manifest, geneType, GeneName.New(geneName), Architecture.New(architecture));

        hash.Should().BeNone();
    }

    [Theory]
    [InlineData(GeneType.Catlet, "catlet", "any")]
    [InlineData(GeneType.Fodder, "first-food", "hyperv/amd64")]
    [InlineData(GeneType.Fodder, "second-food", "hyperv/any")]
    [InlineData(GeneType.Fodder, "third-food", "any")]
    [InlineData(GeneType.Volume, "sda", "hyperv/amd64")]
    [InlineData(GeneType.Volume, "sdb", "hyperv/any")]
    [InlineData(GeneType.Volume, "sdc", "any")]
    public void FindBestArchitecture_UsableGeneExists_ReturnsArchitecture(
        GeneType geneType, string geneName, string expectedArchitecture)
    {
        var result = GeneSetTagManifestUtils.FindBestArchitecture(
            _manifest, Architecture.New("hyperv/amd64"), geneType, GeneName.New(geneName));

        result.Should().BeRight().Which.Should().BeSome()
            .Which.Should().Be(Architecture.New(expectedArchitecture));
    }

    [Theory]
    [InlineData(GeneType.Fodder, "invalid-food")]
    [InlineData(GeneType.Volume, "sdz")]
    public void FindBestArchitecture_UsableGeneDoesNotExist_ReturnsNone(
        GeneType geneType, string geneName)
    {
        var result = GeneSetTagManifestUtils.FindBestArchitecture(
            _manifest, Architecture.New("hyperv/amd64"), geneType, GeneName.New(geneName));

        result.Should().BeRight().Which.Should().BeNone();
    }

    [Theory]
    [InlineData(GeneType.Catlet, "catlet")]
    [InlineData(GeneType.Fodder, "first-food")]
    [InlineData(GeneType.Volume, "sda")]
    public void FindBestArchitecture_NoGenesInManifest_ReturnsNone(
        GeneType geneType, string geneName)
    {
        var result = GeneSetTagManifestUtils.FindBestArchitecture(
            new GenesetTagManifestData(), Architecture.New("hyperv/amd64"),
            geneType, GeneName.New(geneName));

        result.Should().BeRight().Which.Should().BeNone();
    }
}
