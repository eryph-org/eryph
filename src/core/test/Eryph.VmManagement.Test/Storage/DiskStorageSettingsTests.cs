using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.VmAgent;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Storage;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt.Common;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test.Storage;

public class DiskStorageSettingsTests
{
    private readonly VmHostAgentConfiguration _vmHostAgentConfig = new()
    {
        Defaults = new VmHostAgentDefaultsConfiguration
        {
            Volumes = @"x:\disks\",
        },
        Datastores =
        [
            new VmHostAgentDataStoreConfiguration()
            {
                Name = "prod",
                Path = @"x:\prod-disks\",
            }
        ]
    };

    [Theory]
    [InlineData("any", @"x:\disks\genepool\acme\acme-os\1.0\volumes")]
    [InlineData("hyperv/any", @"x:\disks\genepool\acme\acme-os\1.0\volumes\hyperv")]
    [InlineData("hyperv/amd64", @"x:\disks\genepool\acme\acme-os\1.0\volumes\hyperv\amd64")]
    public void FromSourceString_ValidGenepoolSource_ReturnsSettings(
        string architecture,
        string expectedPath)
    {
        var uniqueGeneId = new UniqueGeneIdentifier(
            GeneType.Volume,
            GeneIdentifier.New("gene:acme/acme-os/1.0:sda"),
            Architecture.New(architecture));

        var result = DiskStorageSettings.FromSourceString(
            _vmHostAgentConfig, Seq1(uniqueGeneId), "gene:acme/acme-os/1.0:sda");

        var resultSettings = result.Should().BeSome().Subject;
        resultSettings.DiskIdentifier.Should().Be(Guid.Empty);
        resultSettings.FileName.Should().Be("sda.vhdx");
        resultSettings.Generation.Should().Be(0);
        resultSettings.Name.Should().Be("sda");
        resultSettings.Path.Should().Be(expectedPath);
        resultSettings.StorageIdentifier.Should().BeNone();
        resultSettings.StorageNames.ProjectName.Should().BeSome()
            .Which.Should().Be(EryphConstants.DefaultDataStoreName);
        resultSettings.StorageNames.EnvironmentName.Should().BeSome()
            .Which.Should().Be(EryphConstants.DefaultEnvironmentName);
        resultSettings.StorageNames.DataStoreName.Should().BeSome()
            .Which.Should().Be(EryphConstants.DefaultDataStoreName);
        resultSettings.Gene.Should().BeSome().Which.Should().Be(uniqueGeneId);
    }

    [Fact]
    public void FromSourceString_ValidDiskPathInEryphStorage_ReturnsSettings()
    {
        var result = DiskStorageSettings.FromSourceString(
            _vmHostAgentConfig, Empty, @"x:\prod-disks\test\test-disk.vhdx");

        var resultSettings = result.Should().BeSome().Subject;
        resultSettings.DiskIdentifier.Should().Be(Guid.Empty);
        resultSettings.FileName.Should().Be("test-disk.vhdx");
        resultSettings.Generation.Should().Be(0);
        resultSettings.Name.Should().Be("test-disk");
        resultSettings.Path.Should().Be(@"x:\prod-disks\test");
        resultSettings.StorageIdentifier.Should().BeSome().Which.Should().Be("test");
        resultSettings.StorageNames.ProjectName.Should().BeSome()
            .Which.Should().Be(EryphConstants.DefaultDataStoreName);
        resultSettings.StorageNames.EnvironmentName.Should().BeSome()
            .Which.Should().Be(EryphConstants.DefaultEnvironmentName);
        resultSettings.StorageNames.DataStoreName.Should().BeSome()
            .Which.Should().Be("prod");
        resultSettings.Gene.Should().BeNone();
    }

    [Fact]
    public void FromSourceString_ValidOtherDiskPath_ReturnsSettings()
    {
        var result = DiskStorageSettings.FromSourceString(
            _vmHostAgentConfig, Empty, @"x:\other-disks\test\test-disk.vhdx");

        var resultSettings = result.Should().BeSome().Subject;
        resultSettings.DiskIdentifier.Should().Be(Guid.Empty);
        resultSettings.FileName.Should().Be("test-disk.vhdx");
        resultSettings.Generation.Should().Be(0);
        resultSettings.Name.Should().Be("test-disk");
        resultSettings.Path.Should().Be(@"x:\other-disks\test");
        resultSettings.StorageIdentifier.Should().BeNone();
        resultSettings.StorageNames.ProjectName.Should().BeNone();
        resultSettings.StorageNames.EnvironmentName.Should().BeNone();
        resultSettings.StorageNames.DataStoreName.Should().BeNone();
        resultSettings.Gene.Should().BeNone();
    }

    [Fact]
    public async Task FromVhdPath_Resolved_Name_without_generation()
    {
        var path = @"x:\disks\sda_g2.vhdx";
        var mapping = new FakeTypeMapping();
        var psEngine = new TestPowershellEngine(mapping);
        psEngine.GetObjectCallback = (_, command) =>
        {
            if (!command.ToString().StartsWith("Get-VHD"))
                return Error.New($"Unexpected command: {command}.");

            if (command.ToString().Contains("sda_g2"))
            {
                return Seq1(psEngine.ToPsObject<object>(new VhdInfo
                {
                    ParentPath = @"x:\dummy\sda_g1.vhdx",
                    Path = path,
                }));
            }

            if (command.ToString().Contains("sda_g1"))
            {
                return Seq1(psEngine.ToPsObject<object>(new VhdInfo
                {
                    ParentPath = @"x:\dummy\sda.vhdx",
                    Path = @"x:\dummy\sda_g1.vhdx",
                }));
            }

            return Seq1(psEngine.ToPsObject<object>(new VhdInfo
            {
                Path = @"x:\dummy\sda.vhdx",
            }));
        };
        psEngine.GetValuesCallback = (_, command) =>
        {
            if (!command.ToString().StartsWith("Test-VHD"))
                return Error.New($"Unexpected command: {command}.");
            return Seq1<object>(true);
        };

        var result = await DiskStorageSettings.FromVhdPath(
            psEngine, _vmHostAgentConfig, path);

        var resultSettings = result.Should().BeRight().Subject;
        resultSettings.Name.Should().Be("sda");
        resultSettings.Generation.Should().Be(2);
    }

    [Fact]
    public async Task FromVhdPath_ValidGenepoolDisk_ReturnsSettings()
    {
        var path = @"x:\disks\genepool\acme\acme-os\1.0\volumes\sda.vhdx";
        var mapping = new FakeTypeMapping();
        var psEngine = new TestPowershellEngine(mapping);
        psEngine.GetObjectCallback = (_, command) =>
        {
            if (!command.ToString().StartsWith("Get-VHD"))
                return Error.New($"Unexpected command: {command}.");

            return Seq1(psEngine.ToPsObject<object>(new VhdInfo
            {
                Path = path,
            }));
        };
        psEngine.GetValuesCallback = (_, command) =>
        {
            if (!command.ToString().StartsWith("Test-VHD"))
                return Error.New($"Unexpected command: {command}.");
            return Seq1<object>(true);
        };

        var result = await DiskStorageSettings.FromVhdPath(
            psEngine, _vmHostAgentConfig, path);

        var resultSettings = result.Should().BeRight().Subject;
        resultSettings.Name.Should().Be("sda");
        resultSettings.Generation.Should().Be(0);
        resultSettings.StorageNames.ProjectName.Should().BeSome()
            .Which.Should().Be(EryphConstants.DefaultProjectName);
        resultSettings.StorageNames.EnvironmentName.Should().BeSome()
            .Which.Should().Be(EryphConstants.DefaultEnvironmentName);
        resultSettings.StorageNames.DataStoreName.Should().BeSome()
            .Which.Should().Be(EryphConstants.DefaultDataStoreName);
        resultSettings.StorageIdentifier.Should().BeNone();
        
        var resultGeneId = resultSettings.Gene.Should().BeSome().Subject;
        resultGeneId.GeneType.Should().Be(GeneType.Volume);
        resultGeneId.Id.GeneSet.Should().Be(GeneSetIdentifier.New("acme/acme-os/1.0"));
        resultGeneId.Id.GeneName.Should().Be(GeneName.New("sda"));
        resultGeneId.Architecture.Should().Be(Architecture.New("any"));
    }
}
