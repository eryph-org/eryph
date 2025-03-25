using Eryph.VmManagement.Storage;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Xunit;

namespace Eryph.VmManagement.Test.Storage;

public class DiskGenerationNamesTests
{
    [Fact]
    public void AddGenerationSuffix_GenerationZero_ReturnsPathWithoutSuffix()
    {
        var result = DiskGenerationNames.AddGenerationSuffix(@"Z:\disks\sda.vhdx", 0);

        result.Should().BeRight()
            .Which.Should().Be(@"Z:\disks\sda.vhdx");
    }

    [Fact]
    public void AddGenerationSuffix_GenerationOne_ReturnsPathWithSuffix()
    {
        var result = DiskGenerationNames.AddGenerationSuffix(@"Z:\disks\sda.vhdx", 1);

        result.Should().BeRight()
            .Which.Should().Be(@"Z:\disks\sda_g1.vhdx");
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"Z:\disks\")]
    public void AddGenerationSuffix_InvalidPath_ReturnsError(string path)
    {
        var result = DiskGenerationNames.AddGenerationSuffix(path, 1);

        result.Should().BeLeft()
            .Which.Message.Should().Be($"The disk path '{path}' is invalid.");
    }

    [Fact]
    public void GetFileNameWithoutSuffix_NoParentPath_ReturnsUnmodifiedName()
    {
        var result = DiskGenerationNames.GetFileNameWithoutSuffix(
            @"Z:\disks\sda_g1.vhdx", null);

        result.Should().BeRight().Which.Should().Be("sda_g1");
    }

    [Theory]
    [InlineData(@"Z:\disks\sda.vhdx")]
    [InlineData(@"Z:\disks\sda_g1.vhdx")]
    public void GetFileNameWithoutSuffix_MismatchedParentGeneration_ReturnsUnmodifiedName(
        string parentPath)
    {
        var result = DiskGenerationNames.GetFileNameWithoutSuffix(
            @"Z:\disks\sda_g3.vhdx", parentPath);

        result.Should().BeRight().Which.Should().Be("sda_g3");
    }

    [Theory]
    [InlineData(@"Z:\disks\sda_g1.vhdx", @"Z:\disks\sda.vhdx")]
    [InlineData(@"Z:\disks\sda_g3.vhdx", @"Z:\disks\sda_g2.vhdx")]
    public void GetFileNameWithoutSuffix_CorrectParentGeneration_ReturnsNameWithoutGeneration(
        string path,
        string parentPath)
    {
        var result = DiskGenerationNames.GetFileNameWithoutSuffix(
            path, parentPath);

        result.Should().BeRight().Which.Should().Be("sda");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"Z:\disks\")]
    public void GetFileNameWithoutSuffix_InvalidPath_ReturnsError(
        string? path)
    {
        var result = DiskGenerationNames.GetFileNameWithoutSuffix(
            path, @"Z:\disks\sda.vhdx");

        result.Should().BeLeft()
            .Which.Message.Should().Be($"The disk path '{path}' is invalid.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"Z:\disks\")]
    public void GetFileNameWithoutSuffix_InvalidParentPath_ReturnsError(
        string? parentPath)
    {
        var result = DiskGenerationNames.GetFileNameWithoutSuffix(
            @"Z:\disks\sda.vhdx", parentPath);

        result.Should().BeLeft()
            .Which.Message.Should().Be($"The parent disk path '{parentPath}' is invalid.");
    }
}
