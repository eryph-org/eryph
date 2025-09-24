using System.Security.Cryptography;
using System.Text;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using Eryph.Modules.GenePool.Genetics;

namespace Eryph.Modules.GenePoolModule.Test.Genetics;

public class GeneSetTagManifestUtilsTests
{
    private readonly GenesetTagManifestData _manifest = new()
    {
        Geneset = "acme/acme-os/1.0",
        CatletGene = ComputeHash("catlet"),
        FodderGenes =
        [
            new GeneReferenceData()
            {
                Name = "first-food",
                Architecture = "any",
                Hash = ComputeHash("first-food-any"),
            },
            new GeneReferenceData()
            {
                Name = "first-food",
                Architecture = "hyperv/any",
                Hash = ComputeHash("first-food-hyperv-any"),
            },
            new GeneReferenceData()
            {
                Name = "first-food",
                Architecture = "hyperv/amd64",
                Hash = ComputeHash("first-food-hyperv-amd64"),
            },
            new GeneReferenceData()
            {
                Name = "second-food",
                Architecture = "any",
                Hash = ComputeHash("second-food-any"),
            },
            new GeneReferenceData()
            {
                Name = "second-food",
                Architecture = "hyperv/any",
                Hash = ComputeHash("second-food-hyperv-any"),
            },
            new GeneReferenceData()
            {
                Name = "third-food",
                Architecture = "any",
                Hash = ComputeHash("third-food-any"),
            },
        ],
        VolumeGenes =
        [
            new GeneReferenceData()
            {
                Name = "sda",
                Architecture = "any",
                Hash = ComputeHash("sda-any"),
            },
            new GeneReferenceData()
            {
                Name = "sda",
                Architecture = "hyperv/any",
                Hash = ComputeHash("sda-hyperv-any"),
            },
            new GeneReferenceData()
            {
                Name = "sda",
                Architecture = "hyperv/amd64",
                Hash = ComputeHash("sda-hyperv-amd64"),
            },
            new GeneReferenceData()
            {
                Name = "sdb",
                Architecture = "any",
                Hash = ComputeHash("sdb-any"),
            },
            new GeneReferenceData()
            {
                Name = "sdb",
                Architecture = "hyperv/any",
                Hash = ComputeHash("sdb-hyperv-any"),
            },
            new GeneReferenceData()
            {
                Name = "sdc",
                Architecture = "any",
                Hash = ComputeHash("sdc-any"),
            }
        ],
    };

    [Fact]
    public void GetGenes_ValidGenes_ReturnsGenes()
    {
        var result = GeneSetTagManifestUtils.GetGenes(_manifest).Should().BeRight().Subject;

        var dictionary = result.ToDictionary();
        dictionary.Should().HaveCount(13);

        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("catlet::gene:acme/acme-os/1.0:catlet[any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("catlet")));

        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:first-food[any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("first-food-any")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:first-food[hyperv/any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("first-food-hyperv-any")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:first-food[hyperv/amd64]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("first-food-hyperv-amd64")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:second-food[any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("second-food-any")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:second-food[hyperv/any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("second-food-hyperv-any")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("fodder::gene:acme/acme-os/1.0:third-food[any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("third-food-any")));

        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sda[any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("sda-any")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sda[hyperv/any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("sda-hyperv-any")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sda[hyperv/amd64]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("sda-hyperv-amd64")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sdb[any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("sdb-any")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sdb[hyperv/any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("sdb-hyperv-any")));
        dictionary.Should().ContainKey(UniqueGeneIdentifier.New("volume::gene:acme/acme-os/1.0:sdc[any]"))
            .WhoseValue.Should().Be(GeneHash.New(ComputeHash("sdc-any")));
    }

    private static string ComputeHash(string value) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";
}
