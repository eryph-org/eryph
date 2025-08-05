using System.Security.Cryptography;
using System.Text;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core.Tests.Genetics;

public class CatletGeneResolvingTests
{
    private readonly HashMap<UniqueGeneIdentifier, GeneHash> _genes = HashMap(
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
    public void ResolveGenes_GeneExists_ReturnsGene(
        GeneType geneType, string geneName, string architecture, string expectedContent)
    {
        var geneSetId = GeneSetIdentifier.New("acme/acme-os/1.0");
        var geneId = new GeneIdentifier(geneSetId, GeneName.New(geneName));
        var geneIdAndType = new GeneIdentifierWithType(geneType, geneId);

        var expectedUniqueId = new UniqueGeneIdentifier(geneType, geneId, Architecture.New(architecture));
        var expectedGeneHash = GeneHash.New(ComputeHash(expectedContent));

        var either = CatletGeneResolving.ResolveGenes(Seq1(geneIdAndType), Architecture.New(architecture), _genes);

        var result = either.Should().BeRight().Subject.ToDictionary();
        result.Should().HaveCount(1);
        result.Should().ContainKey(expectedUniqueId)
            .WhoseValue.Should().Be(expectedGeneHash);
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

        var either = CatletGeneResolving.ResolveGenes(Seq1(geneIdAndType), Architecture.New(architecture), _genes);

        var result = either.Should().BeRight().Subject.ToDictionary();
        result.Should().HaveCount(1);
        result.Should().ContainKey(expectedUniqueId)
            .WhoseValue.Should().Be(expectedGeneHash);
    }

    [Theory]
    [InlineData(GeneType.Catlet, "catlet", "any", "catlet")]
    [InlineData(GeneType.Fodder, "first-food", "hyperv/amd64", "first-food-hyperv-amd64")]
    [InlineData(GeneType.Fodder, "second-food", "hyperv/any", "second-food-hyperv-any")]
    [InlineData(GeneType.Fodder, "third-food", "any", "third-food-any")]
    [InlineData(GeneType.Volume, "sda", "hyperv/amd64", "sda-hyperv-amd64")]
    [InlineData(GeneType.Volume, "sdb", "hyperv/any", "sdb-hyperv-any")]
    [InlineData(GeneType.Volume, "sdc", "any", "sdc-any")]
    public void ResolveGenes_UsableGeneExists_ReturnsArchitecture(
        GeneType geneType, string geneName, string expectedArchitecture, string expectedContent)
    {
        var geneSetId = GeneSetIdentifier.New("acme/acme-os/1.0");
        var geneId = new GeneIdentifier(geneSetId, GeneName.New(geneName));
        var geneIdAndType = new GeneIdentifierWithType(geneType, geneId);
        var architecture = Architecture.New("hyperv/amd64");

        var expectedUniqueId = new UniqueGeneIdentifier(geneType, geneId, Architecture.New(expectedArchitecture));
        var expectedGeneHash = GeneHash.New(ComputeHash(expectedContent));

        var either = CatletGeneResolving.ResolveGenes(Seq1(geneIdAndType), architecture, _genes);

        var result = either.Should().BeRight().Subject.ToDictionary();
        result.Should().HaveCount(1);
        result.Should().ContainKey(expectedUniqueId)
            .WhoseValue.Should().Be(expectedGeneHash);
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

        var either = CatletGeneResolving.ResolveGenes(Seq1(geneIdAndType), Architecture.New(architecture), _genes);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve some genes.");
        error.Inner.Should().BeSome().Which.Message
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

        var either = CatletGeneResolving.ResolveGenes(Seq1(geneIdAndType), Architecture.New(architecture), _genes);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve some genes.");
        error.Inner.Should().BeSome().Which.Message
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

        var either = CatletGeneResolving.ResolveGenes(Seq1(geneIdAndType), Architecture.New(architecture), _genes);

        var error = either.Should().BeLeft().Subject;
        error.Message.Should().Be("Could not resolve some genes.");
        error.Inner.Should().BeSome().Which.Message
            .Should().Match($"The gene {geneType.ToString().ToLowerInvariant()}::{geneId} is not compatible with the processor architecture {Architecture.New(architecture).ProcessorArchitecture}.");
    }

    [Theory, PairwiseData]
    public void ResolveGeneSetIdentifiers_ValidIdentifiers_ResolvesAllIdentifiers(
        [CombinatorialValues("acme/acme-os", "acme/acme-os/latest", "acme/acme-os/1.0")]
        string parentGeneSet,
        [CombinatorialValues("acme/acme-images", "acme/acme-images/latest", "acme/acme-images/1.0")]
        string driveGeneSet,
        [CombinatorialValues("acme/acme-tools", "acme/acme-tools/latest", "acme/acme-tools/1.0")]
        string fodderGeneSet)
    {
        var config = new CatletConfig
        {
            Parent = parentGeneSet,
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = $"gene:{driveGeneSet}:test-image",
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Source = $"gene:{fodderGeneSet}:test-fodder",
                }
            ],
        };

        var geneSetMap = HashMap(
            (GeneSetIdentifier.New("acme/acme-os/latest"), GeneSetIdentifier.New("acme/acme-os/1.0")),
            (GeneSetIdentifier.New("acme/acme-os/1.0"), GeneSetIdentifier.New("acme/acme-os/1.0")),
            (GeneSetIdentifier.New("acme/acme-images/latest"), GeneSetIdentifier.New("acme/acme-images/1.0")),
            (GeneSetIdentifier.New("acme/acme-images/1.0"), GeneSetIdentifier.New("acme/acme-images/1.0")),
            (GeneSetIdentifier.New("acme/acme-tools/latest"), GeneSetIdentifier.New("acme/acme-tools/1.0")),
            (GeneSetIdentifier.New("acme/acme-tools/1.0"), GeneSetIdentifier.New("acme/acme-tools/1.0")));


        var result = CatletGeneResolving.ResolveGeneSetIdentifiers(config, geneSetMap);

        var resultConfig = result.Should().BeRight().Subject;
        resultConfig.Parent.Should().Be("acme/acme-os/1.0");
        resultConfig.Drives.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:acme/acme-images/1.0:test-image"));
        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder"));
    }

    [Fact]
    public void ResolveGeneSetIdentifiers_UnresolvedParent_ReturnsError()
    {
        var config = new CatletConfig
        {
            Parent = "acme/acme-os/latest",
        };

        var result = CatletGeneResolving.ResolveGeneSetIdentifiers(config, HashMap<GeneSetIdentifier, GeneSetIdentifier>());

        result.Should().BeLeft().Which.Message
            .Should().Be("The gene set 'acme/acme-os/latest' could not be resolved.");
    }

    [Fact]
    public void ResolveGeneSetIdentifiers_UnresolvedDriveSource_ReturnsError()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = "gene:acme/acme-images/latest:test-image",
                }
            ],
        };

        var result = CatletGeneResolving.ResolveGeneSetIdentifiers(config, HashMap<GeneSetIdentifier, GeneSetIdentifier>());

        result.Should().BeLeft().Which.Message
            .Should().Be("The gene set 'acme/acme-images/latest' could not be resolved.");
    }

    [Fact]
    public void ResolveGeneSetIdentifiers_UnresolvedFodderSource_ReturnsError()
    {
        var config = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/acme-tools/latest:test-fodder",
                }
            ],
        };

        var result = CatletGeneResolving.ResolveGeneSetIdentifiers(config, HashMap<GeneSetIdentifier, GeneSetIdentifier>());

        result.Should().BeLeft().Which.Message
            .Should().Be("The gene set 'acme/acme-tools/latest' could not be resolved.");
    }

    [Fact]
    public void ResolveGeneSetIdentifiers_DriveSourceIsAPath_ReturnsOriginalSource()
    {
        const string source = @"Z:\test-folder\test.vhdx";

        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = source,
                }
            ],
        };

        var result = CatletGeneResolving.ResolveGeneSetIdentifiers(config, HashMap<GeneSetIdentifier, GeneSetIdentifier>());

        result.Should().BeRight().Which.Drives.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be(source));
    }


    private static string ComputeHash(string value) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";
}
