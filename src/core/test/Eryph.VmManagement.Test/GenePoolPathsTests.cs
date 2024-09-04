using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Xunit;

namespace Eryph.VmManagement.Test;

public class GenePoolPathsTests
{
    [Fact]
    public void GetGenePoolPath_ReturnsCorrectPath()
    {
        var vmHostAgentConfig = new VmHostAgentConfiguration
        {
            Defaults = new VmHostAgentDefaultsConfiguration
            {
                Vms = @"Z:\vms",
                Volumes = @"Z:\volumes",
            }
        };

        var result = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig);

        result.Should().Be(@"Z:\volumes\genepool");
    }

    [Fact]
    public void GetGeneSetPath_ReturnsCorrectPath()
    {
        var result = GenePoolPaths.GetGeneSetPath(
            @"Z:\volumes\genepool",
            GeneSetIdentifier.New("acme/acme-os/1.0"));

        result.Should().Be(@"Z:\volumes\genepool\acme\acme-os\1.0");
    }

    [Fact]
    public void GetGeneSetManifestPath_ReturnsCorrectPath()
    {
        var result = GenePoolPaths.GetGeneSetManifestPath(
            @"Z:\volumes\genepool",
            GeneSetIdentifier.New("acme/acme-os/1.0"));

        result.Should().Be(@"Z:\volumes\genepool\acme\acme-os\1.0\manifest-tag.json");
    }

    [Theory]
    [InlineData(
        GeneType.Catlet,
        "gene:acme/acme-os/1.0:catlet",
        @"Z:\volumes\genepool\acme\acme-os\1.0\catlet.json")]
    [InlineData(
        GeneType.Volume,
        "gene:acme/acme-os/1.0:sda",
        @"Z:\volumes\genepool\acme\acme-os\1.0\volumes\sda.vhdx")]
    [InlineData(
        GeneType.Fodder,
        "gene:acme/acme-fodder/1.0:test-fodder",
        @"Z:\volumes\genepool\acme\acme-fodder\1.0\fodder\test-fodder.json")]
    public void GetGenePath_ReturnsCorrectPath(
        GeneType geneType,
        string geneId,
        string expected)
    {
        var result = GenePoolPaths.GetGenePath(
            @"Z:\volumes\genepool",
            geneType,
            GeneIdentifier.New(geneId));

        result.Should().Be(expected);
    }

    [Fact]
    public void GetGeneSetIdFromPath_ValidPath_ReturnsId()
    {
        var result = GenePoolPaths.GetGeneSetIdFromPath(
            @"Z:\volumes\genepool",
            @"Z:\volumes\genepool\acme\acme-os\1.0");

        result.Should().BeRight().Which.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
    }

    [Theory]
    [InlineData(@"a\b")]
    [InlineData(@"Z:\a\b")]
    [InlineData(@"Z:\volumes\b")]
    [InlineData(@"Z:\volumes\genepool\a\b\c\d")]
    public void GetGeneSetIdFromPath_InvalidPath_ReturnsError(
        string geneSetPath)
    {
        var result = GenePoolPaths.GetGeneSetIdFromPath(
            @"Z:\volumes\genepool",
            geneSetPath);

        result.Should().BeLeft();
    }

    [Fact]
    public void GetGeneSetIdFromManifestPath_ValidPath_ReturnsId()
    {
        var result = GenePoolPaths.GetGeneSetIdFromManifestPath(
            @"Z:\volumes\genepool",
            @"Z:\volumes\genepool\acme\acme-os\1.0\geneset-tag.json");

        result.Should().BeRight().Which.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
    }

    [Theory]
    [InlineData(@"a\b")]
    [InlineData(@"Z:\a\b")]
    [InlineData(@"Z:\volumes\b")]
    [InlineData(@"Z:\volumes\genepool\a\b\c\d")]
    [InlineData(@"Z:\volumes\genepool\acme\acme-os\1.0\invalid.json")]
    public void GetGeneSetIdFromManifestPath_InvalidPath_ReturnsError(
        string geneSetPath)
    {
        var result = GenePoolPaths.GetGeneSetIdFromManifestPath(
            @"Z:\volumes\genepool",
            geneSetPath);

        result.Should().BeLeft();
    }
}
