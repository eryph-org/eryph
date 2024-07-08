using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

using static LanguageExt.Prelude;

namespace Eryph.Core.Tests.Genetics;

public class CatletGeneResolvingTests
{
    [Theory, PairwiseData]
    public void ResolveGenesetIdentifiers_ValidIdentifiers_ResolvesAllIdentifiers(
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


        var result = CatletGeneResolving.ResolveGenesetIdentifiers(config, geneSetMap);

        var resultConfig = result.Should().BeRight().Subject;
        resultConfig.Parent.Should().Be("acme/acme-os/1.0");
        resultConfig.Drives.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:acme/acme-images/1.0:test-image"));
        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Source.Should().Be("gene:acme/acme-tools/1.0:test-fodder"));
    }

    [Fact]
    public void ResolveGenesetIdentifiers_UnresolvedParent_ReturnsError()
    {
        var config = new CatletConfig
        {
            Parent = "acme/acme-os/latest",
        };

        var result = CatletGeneResolving.ResolveGenesetIdentifiers(config, HashMap<GeneSetIdentifier, GeneSetIdentifier>());

        result.Should().BeLeft().Which.Message
            .Should().Be("The gene set 'acme/acme-os/latest' could not be resolved.");
    }

    [Fact]
    public void ResolveGenesetIdentifiers_UnresolvedDriveSource_ReturnsError()
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

        var result = CatletGeneResolving.ResolveGenesetIdentifiers(config, HashMap<GeneSetIdentifier, GeneSetIdentifier>());

        result.Should().BeLeft().Which.Message
            .Should().Be("The gene set 'acme/acme-images/latest' could not be resolved.");
    }

    [Fact]
    public void ResolveGenesetIdentifiers_UnresolvedFodderSource_ReturnsError()
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

        var result = CatletGeneResolving.ResolveGenesetIdentifiers(config, HashMap<GeneSetIdentifier, GeneSetIdentifier>());

        result.Should().BeLeft().Which.Message
            .Should().Be("The gene set 'acme/acme-tools/latest' could not be resolved.");
    }
}
