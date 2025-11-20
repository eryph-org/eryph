using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Storage;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using LanguageExt.Common;
using Moq;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.HyperV.Test.Storage;

public class VMDriveStorageSettingsTests
{
    private readonly Mock<Func<string, EitherAsync<Error, Option<VhdInfo>>>> _getVhdInfoMock = new();
    private readonly Mock<Func<string, EitherAsync<Error, bool>>> _testVhdMock = new();

    private readonly VmHostAgentConfiguration _vmHostAgentConfiguration = new()
    {
        Defaults = new()
        {
            Vms = @"x:\data",
            Volumes = @"x:\disks",
        }
    };

    private readonly VMStorageSettings _storageSettings = new()
    {
        StorageIdentifier = Some("storage-id-vm"),
        StorageNames = new()
        {
            DataStoreName = Some("default"),
            EnvironmentName = Some("default"),
            ProjectName = Some("default"),
        },
    };

    [Fact]
    public async Task PlanDriveStorageSettings_MultipleDrives_DrivesHaveCorrectControllerLocations()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Vhd,
                    Store = EryphConstants.DefaultDataStoreName,
                    Name = "sda",
                },
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Dvd,
                    Source = @"x:\dvds\disk1.iso",
                },
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Vhd,
                    Store = EryphConstants.DefaultDataStoreName,
                    Name = "sdb",
                },
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Dvd,
                    Source = @"x:\dvds\disk2.iso",
                }
            ],
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));
        _testVhdMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, bool>(false));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings,
            _getVhdInfoMock.Object, _testVhdMock.Object, Empty);


        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);
                dss.Should().BeOfType<HardDiskDriveStorageSettings>()
                    .Which.DiskSettings.FileName.Should().Be("sda.vhdx");
            },
            dss =>
            {
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(1);
                dss.Should().BeOfType<VMDvDStorageSettings>()
                    .Which.Path.Should().Be(@"x:\dvds\disk1.iso");
            },
            dss =>
            {
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(2);
                dss.Should().BeOfType<HardDiskDriveStorageSettings>()
                    .Which.DiskSettings.FileName.Should().Be("sdb.vhdx");
            },
            dss =>
            {
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(3);
                dss.Should().BeOfType<VMDvDStorageSettings>()
                    .Which.Path.Should().Be(@"x:\dvds\disk2.iso");
            });
    }

    [Theory]
    [InlineData(CatletDriveType.Vhd, ".vhdx")]
    [InlineData(CatletDriveType.SharedVhd, ".vhdx")]
    [InlineData(CatletDriveType.VhdSet, ".vhds")]
    public async Task PlanDriveStorageSettings_UsesCorrectExtensionForVhdType(
        CatletDriveType driveType,
        string expectedExtension)
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Type = driveType,
                    Name = "sda",
                    Store = EryphConstants.DefaultDataStoreName,
                }
            ],
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));
        _testVhdMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, bool>(false));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings,
            _getVhdInfoMock.Object, _testVhdMock.Object, Empty);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);

                var hdss = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                hdss.AttachPath.Should().Be($@"x:\disks\storage-id-vm\sda{expectedExtension}");
                hdss.DiskSettings.FileName.Should().Be($"sda{expectedExtension}");
                hdss.DiskSettings.Path.Should().Be(@"x:\disks\storage-id-vm");
            });
    }

    [Theory]
    [InlineData("any", @"x:\disks\genepool\testorg\testset\testtag\volumes\sda.vhdx")]
    [InlineData("hyperv/any", @"x:\disks\genepool\testorg\testset\testtag\volumes\hyperv\sda.vhdx")]
    [InlineData("hyperv/amd64", @"x:\disks\genepool\testorg\testset\testtag\volumes\hyperv\amd64\sda.vhdx")]
    public async Task PlanDriveStorageSettings_NewDiskWithoutConfiguredSizeAndWithParent_UsesParentSize(
        string architecture,
        string expectedParentPath)
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Vhd,
                    Name = "sda",
                    Store = EryphConstants.DefaultDataStoreName,
                    Source = "gene:testorg/testset/testtag:sda",
                }
            ],
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        _getVhdInfoMock.Setup(m => m(expectedParentPath))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 42,
            }));

        _testVhdMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, bool>(false));
        _testVhdMock.Setup(m => m(expectedParentPath))
            .Returns(RightAsync<Error, bool>(true));

        var resolvedGenes = Seq1(new UniqueGeneIdentifier(
            GeneType.Volume,
            GeneIdentifier.New("gene:testorg/testset/testtag:sda"),
            Architecture.New(architecture)));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings,
            _getVhdInfoMock.Object, _testVhdMock.Object, resolvedGenes);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.Vhd);
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);
                
                var settings = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                settings.AttachPath.Should().Be(@"x:\disks\storage-id-vm\sda_g1.vhdx");
                settings.DiskSettings.SizeBytes.Should().BeNull();
                settings.DiskSettings.SizeBytesCreate.Should().Be(42);

                AssertParent(settings.DiskSettings.ParentSettings, expectedParentPath);
            });
    }

    [Fact]
    public async Task PlanDriveStorageSettings_NewDiskWithoutConfiguredSizeAndWithoutParent_UsesDefaultSize()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Vhd,
                    Name = "sda",
                    Store = EryphConstants.DefaultDataStoreName,
                }
            ],
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));
        _testVhdMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, bool>(false));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings,
            _getVhdInfoMock.Object, _testVhdMock.Object, Empty);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.Vhd);
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);

                var settings = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                settings.AttachPath.Should().Be(@"x:\disks\storage-id-vm\sda.vhdx");
                settings.DiskSettings.SizeBytes.Should().BeNull();
                settings.DiskSettings.SizeBytesCreate.Should().Be(1*1024L*1024*1024);

                settings.DiskSettings.ParentSettings.Should().BeNone();
            });
    }

    [Fact]
    public async Task PlanDriveStorageSettings_NewDiskWithConfiguredSizeAndWithoutParent_UsesConfiguredSize()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Vhd,
                    Name = "sda",
                    Store = EryphConstants.DefaultDataStoreName,
                    Size = 42,
                }
            ],
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));
        _testVhdMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, bool>(false));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings,
            _getVhdInfoMock.Object, _testVhdMock.Object, Empty);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.Vhd);
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);

                var settings = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                settings.AttachPath.Should().Be(@"x:\disks\storage-id-vm\sda.vhdx");
                settings.DiskSettings.SizeBytes.Should().Be(42 * 1024L * 1024 * 1024);
                settings.DiskSettings.SizeBytesCreate.Should().Be(42 * 1024L * 1024 * 1024);

                settings.DiskSettings.ParentSettings.Should().BeNone();
            });
    }

    [Theory]
    [InlineData("any", @"x:\disks\genepool\testorg\testset\testtag\volumes\sda.vhdx")]
    [InlineData("hyperv/any", @"x:\disks\genepool\testorg\testset\testtag\volumes\hyperv\sda.vhdx")]
    [InlineData("hyperv/amd64", @"x:\disks\genepool\testorg\testset\testtag\volumes\hyperv\amd64\sda.vhdx")]
    public async Task PlanDriveStorageSettings_NewDiskWithConfiguredSizeAndWithSmallerParent_UsesConfiguredSize(
        string architecture,
        string expectedParentPath)
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Vhd,
                    Name = "sda",
                    Store = EryphConstants.DefaultDataStoreName,
                    Source = "gene:testorg/testset/testtag:sda",
                    Size = 42,
                }
            ],
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        _getVhdInfoMock.Setup(m => m(expectedParentPath))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 40 * 1024L * 1024 * 1024,
            }));

        _testVhdMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, bool>(false));
        _testVhdMock.Setup(m => m(expectedParentPath))
            .Returns(RightAsync<Error, bool>(true));

        var resolvedGenes = Seq1(new UniqueGeneIdentifier(
            GeneType.Volume,
            GeneIdentifier.New("gene:testorg/testset/testtag:sda"),
            Architecture.New(architecture)));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings,
            _getVhdInfoMock.Object, _testVhdMock.Object, resolvedGenes);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.Vhd);
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);

                var settings = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                settings.AttachPath.Should().Be(@"x:\disks\storage-id-vm\sda_g1.vhdx");
                settings.DiskSettings.SizeBytes.Should().Be(42 * 1024L * 1024 * 1024);
                settings.DiskSettings.SizeBytesCreate.Should().Be(42 * 1024L * 1024 * 1024);

                AssertParent(settings.DiskSettings.ParentSettings, expectedParentPath);
            });
    }

    [Fact]
    public async Task PlanDriveStorageSettings_NewDiskWithConfiguredSizeAndWithLargerParent_Fails()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Vhd,
                    Name = "sda",
                    Store = EryphConstants.DefaultDataStoreName,
                    Source = "gene:testorg/testset/testtag:sda",
                    Size = 42,
                }
            ],
        };

        var parentPath = @"x:\disks\genepool\testorg\testset\testtag\volumes\sda.vhdx";

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        _getVhdInfoMock.Setup(m => m(parentPath))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 50 * 1024L * 1024 * 1024,
            }));

        _testVhdMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, bool>(false));
        _testVhdMock.Setup(m => m(parentPath))
            .Returns(RightAsync<Error, bool>(true));

        var resolvedGenes = Seq1(new UniqueGeneIdentifier(
            GeneType.Volume,
            GeneIdentifier.New("gene:testorg/testset/testtag:sda"),
            Architecture.New("any")));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings,
            _getVhdInfoMock.Object, _testVhdMock.Object, resolvedGenes);

        result.Should().BeLeft().Which.Message.Should().Be("Disk size is below minimum size of the virtual disk");
    }

    [Fact]
    public async Task PlanDriveStorageSettings_ExistingDiskWithConfiguredSizeTooSmall_Fails()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Vhd,
                    Store = EryphConstants.DefaultDataStoreName,
                    Name = "sda",
                    Size = 42,
                }
            ],
        };

        var parentPath = @"x:\disks\storage-id-vm\sda.vhdx";
        _getVhdInfoMock.Setup(m => m(parentPath))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 50 * 1024L * 1024 * 1024,
            }));

        _testVhdMock.Setup(m => m(parentPath))
            .Returns(RightAsync<Error, bool>(true));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings,
            _getVhdInfoMock.Object, _testVhdMock.Object, Empty);

        result.Should().BeLeft().Which.Message.Should().Be("Disk size is below minimum size of the virtual disk");
    }

    [Fact]
    public async Task PlanDriveStorageSettings_ExistingDiskWithConfiguredSizeLarger_UsesConfiguredSize()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Type = CatletDriveType.Vhd,
                    Name = "sda",
                    Store = EryphConstants.DefaultDataStoreName,
                    Size = 42,
                }
            ],
        };

        var parentPath = @"x:\disks\storage-id-vm\sda.vhdx";
        _getVhdInfoMock.Setup(m => m(parentPath))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 40 * 1024L * 1024 * 1024,
            }));

        _testVhdMock.Setup(m => m(parentPath))
            .Returns(RightAsync<Error, bool>(true));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings,
            _getVhdInfoMock.Object, _testVhdMock.Object, Empty);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.Vhd);
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);

                var settings = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                settings.AttachPath.Should().Be(@"x:\disks\storage-id-vm\sda.vhdx");
                settings.DiskSettings.SizeBytes.Should().Be(42 * 1024L * 1024 * 1024);
                settings.DiskSettings.SizeBytesCreate.Should().Be(42 * 1024L * 1024 * 1024);

                settings.DiskSettings.ParentSettings.Should().BeNone();
            });
    }

    private void AssertParent(
        Option<DiskStorageSettings> parentSettings,
        string expectedParentPath)
    {
        var settings = parentSettings.Should().BeSome().Subject;
        settings.Path.Should().Be(Path.GetDirectoryName(expectedParentPath));
        settings.Name.Should().Be("sda");
    }
}
