using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.VmManagement;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Xunit;

namespace Eryph.Modules.GenePoolModule.Test;

public class GenePoolPathsTests
{
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

        result.Should().Be(@"Z:\volumes\genepool\acme\acme-os\1.0\geneset-tag.json");
    }

    [Theory]
    [InlineData(
        GeneType.Catlet,
        "gene:acme/acme-os/1.0:catlet",
        "any",
        @"Z:\volumes\genepool\acme\acme-os\1.0\catlet.json")]
    [InlineData(
        GeneType.Volume,
        "gene:acme/acme-os/1.0:sda",
        "any",
        @"Z:\volumes\genepool\acme\acme-os\1.0\volumes\sda.vhdx")]
    [InlineData(
        GeneType.Volume,
        "gene:acme/acme-os/1.0:sda",
        "hyperv/any",
        @"Z:\volumes\genepool\acme\acme-os\1.0\volumes\hyperv\sda.vhdx")]
    [InlineData(
        GeneType.Volume,
        "gene:acme/acme-os/1.0:sda",
        "hyperv/amd64",
        @"Z:\volumes\genepool\acme\acme-os\1.0\volumes\hyperv\amd64\sda.vhdx")]
    [InlineData(
        GeneType.Fodder,
        "gene:acme/acme-fodder/1.0:test-fodder",
        "any",
        @"Z:\volumes\genepool\acme\acme-fodder\1.0\fodder\test-fodder.json")]
    [InlineData(
        GeneType.Fodder,
        "gene:acme/acme-fodder/1.0:test-fodder",
        "hyperv/any",
        @"Z:\volumes\genepool\acme\acme-fodder\1.0\fodder\hyperv\test-fodder.json")]
    [InlineData(
        GeneType.Fodder,
        "gene:acme/acme-fodder/1.0:test-fodder",
        "hyperv/amd64",
        @"Z:\volumes\genepool\acme\acme-fodder\1.0\fodder\hyperv\amd64\test-fodder.json")]
    public void GetGenePath_ReturnsCorrectPath(
        GeneType geneType,
        string geneId,
        string architecture,
        string expected)
    {
        var result = GenePoolPaths.GetGenePath(
            @"Z:\volumes\genepool",
            geneType,
            Architecture.New(architecture),
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

    [Theory]
    [InlineData(
        @"Z:\volumes\genepool\acme\acme-os\1.0\catlet.json",
        GeneType.Catlet,
        "gene:acme/acme-os/1.0:catlet",
        "any")]
    [InlineData(
        @"Z:\volumes\genepool\acme\acme-os\1.0\volumes\sda.vhdx",
        GeneType.Volume,
        "gene:acme/acme-os/1.0:sda",
        "any")]
    [InlineData(
        @"Z:\volumes\genepool\acme\acme-os\1.0\volumes\hyperv\sda.vhdx",
        GeneType.Volume,
        "gene:acme/acme-os/1.0:sda",
        "hyperv/any")]
    [InlineData(
        @"Z:\volumes\genepool\acme\acme-os\1.0\volumes\hyperv\amd64\sda.vhdx",
        GeneType.Volume,
        "gene:acme/acme-os/1.0:sda",
        "hyperv/amd64")]
    [InlineData(
        @"Z:\volumes\genepool\acme\acme-fodder\1.0\fodder\test-fodder.json",
        GeneType.Fodder,
        "gene:acme/acme-fodder/1.0:test-fodder",
        "any")]
    [InlineData(
        @"Z:\volumes\genepool\acme\acme-fodder\1.0\fodder\hyperv\test-fodder.json",
        GeneType.Fodder,
        "gene:acme/acme-fodder/1.0:test-fodder",
        "hyperv/any")]
    [InlineData(
        @"Z:\volumes\genepool\acme\acme-fodder\1.0\fodder\hyperv\amd64\test-fodder.json",
        GeneType.Fodder,
        "gene:acme/acme-fodder/1.0:test-fodder",
        "hyperv/amd64")]
    public void GetUniqueGeneIdFromPath_ValidPath_ReturnsId(
        string path,
        GeneType expectedGeneType,
        string expectedGeneId,
        string expectedArchitecture)
    {
        var result = GenePoolPaths.GetUniqueGeneIdFromPath(
            @"Z:\volumes\genepool", path);

        result.Should().BeRight().Which.Should().Be(
            new UniqueGeneIdentifier(
                expectedGeneType,
                GeneIdentifier.New(expectedGeneId),
                Architecture.New(expectedArchitecture)));
    }

    [Theory]
    [InlineData(@"a\b")]
    [InlineData(@"Z:\a\b")]
    [InlineData(@"Z:\volumes\b")]
    [InlineData(@"Z:\volumes\genepool\a\b\c\d")]
    [InlineData(@"Z:\volumes\genepool\acme\acme-os\1.0\invalid.json")]
    // Catlets are never architecture-specific. Hence, the following paths are invalid.
    [InlineData(@"Z:\volumes\genepool\acme\acme-os\1.0\hyperv\catlet.json")]
    [InlineData(@"Z:\volumes\genepool\acme\acme-os\1.0\hyperv\amd64\catlet.json")]
    public void GetUniqueGeneIdFromPath_InvalidPath_ReturnsError(
        string path)
    {
        var result = GenePoolPaths.GetUniqueGeneIdFromPath(
            @"Z:\volumes\genepool", path);

        result.Should().BeLeft();
    }
}
