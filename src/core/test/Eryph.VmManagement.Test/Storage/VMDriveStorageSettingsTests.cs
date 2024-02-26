using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
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

namespace Eryph.VmManagement.Test.Storage;

public class VMDriveStorageSettingsTests
{
    private readonly Mock<Func<string, EitherAsync<Error, Option<VhdInfo>>>> _getVhdInfoMock = new();

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
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Type = CatletDriveType.VHD,
                    Name = "sda",
                },
                new CatletDriveConfig
                {
                    Type = CatletDriveType.DVD,
                    Source = @"x:\dvds\disk1.iso",
                },
                new CatletDriveConfig
                {
                    Type = CatletDriveType.VHD,
                    Name = "sdb",
                },
                new CatletDriveConfig
                {
                    Type = CatletDriveType.DVD,
                    Source = @"x:\dvds\disk2.iso",
                },
            },
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings, _getVhdInfoMock.Object);


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
    [InlineData(CatletDriveType.VHD, ".vhdx")]
    [InlineData(CatletDriveType.SharedVHD, ".vhdx")]
    [InlineData(CatletDriveType.VHDSet, ".vhds")]
    public async Task PlanDriveStorageSettings_UsesCorrectExtensionForVhdType(
        CatletDriveType driveType,
        string expectedExtension)
    {
        var config = new CatletConfig
        {
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Type = driveType,
                    Name = "sda",
                },
            },
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings, _getVhdInfoMock.Object);

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

    [Fact]
    public async Task PlanDriveStorageSettings_NewDiskWithoutConfiguredSizeAndWithParent_UsesParentSize()
    {
        var config = new CatletConfig
        {
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Type = CatletDriveType.VHD,
                    Name = "sda",
                    Source = "gene:testorg/testset/testtag:sda",
                },
            },
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        _getVhdInfoMock.Setup(m => m(@"x:\disks\genepool\testorg\testset\testtag\volumes\sda.vhdx"))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 42,
            }));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
                       _vmHostAgentConfiguration, config, _storageSettings, _getVhdInfoMock.Object);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.VHD);
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);
                
                var settings = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                settings.AttachPath.Should().Be(@"x:\disks\storage-id-vm\sda.vhdx");
                settings.DiskSettings.SizeBytes.Should().BeNull();
                settings.DiskSettings.SizeBytesCreate.Should().Be(42);

                AssertParent(settings.DiskSettings.ParentSettings);
            });
    }

    [Fact]
    public async Task PlanDriveStorageSettings_NewDiskWithoutConfiguredSizeAndWithoutParent_UsesDefaultSize()
    {
        var config = new CatletConfig
        {
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Type = CatletDriveType.VHD,
                    Name = "sda",
                },
            },
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings, _getVhdInfoMock.Object);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.VHD);
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
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Type = CatletDriveType.VHD,
                    Name = "sda",
                    Size = 42,
                },
            },
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings, _getVhdInfoMock.Object);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.VHD);
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);

                var settings = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                settings.AttachPath.Should().Be(@"x:\disks\storage-id-vm\sda.vhdx");
                settings.DiskSettings.SizeBytes.Should().Be(42 * 1024L * 1024 * 1024);
                settings.DiskSettings.SizeBytesCreate.Should().Be(42 * 1024L * 1024 * 1024);

                settings.DiskSettings.ParentSettings.Should().BeNone();
            });
    }

    [Fact]
    public async Task PlanDriveStorageSettings_NewDiskWithConfiguredSizeAndWithSmallerParent_UsesConfiguredSize()
    {
        var config = new CatletConfig
        {
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Type = CatletDriveType.VHD,
                    Name = "sda",
                    Source = "gene:testorg/testset/testtag:sda",
                    Size = 42,
                },
            },
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        _getVhdInfoMock.Setup(m => m(@"x:\disks\genepool\testorg\testset\testtag\volumes\sda.vhdx"))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 40 * 1024L * 1024 * 1024,
            }));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings, _getVhdInfoMock.Object);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.VHD);
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);

                var settings = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                settings.AttachPath.Should().Be(@"x:\disks\storage-id-vm\sda.vhdx");
                settings.DiskSettings.SizeBytes.Should().Be(42 * 1024L * 1024 * 1024);
                settings.DiskSettings.SizeBytesCreate.Should().Be(42 * 1024L * 1024 * 1024);

                AssertParent(settings.DiskSettings.ParentSettings);
            });
    }

    [Fact]
    public async Task PlanDriveStorageSettings_NewDiskWithConfiguredSizeAndWithLargerParent_Fails()
    {
        var config = new CatletConfig
        {
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Type = CatletDriveType.VHD,
                    Name = "sda",
                    Source = "gene:testorg/testset/testtag:sda",
                    Size = 42,
                },
            },
        };

        _getVhdInfoMock.Setup(m => m(It.IsAny<string>()))
            .Returns(RightAsync<Error, Option<VhdInfo>>(None));

        _getVhdInfoMock.Setup(m => m(@"x:\disks\genepool\testorg\testset\testtag\volumes\sda.vhdx"))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 50 * 1024L * 1024 * 1024,
            }));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings, _getVhdInfoMock.Object);

        result.Should().BeLeft().Which.Message.Should().Be("Disk size is below minimum size of the virtual disk");
    }

    [Fact]
    public async Task PlanDriveStorageSettings_ExistingDiskWithConfiguredSizeTooSmall_Fails()
    {
        var config = new CatletConfig
        {
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Type = CatletDriveType.VHD,
                    Name = "sda",
                    Size = 42,
                },
            },
        };

        _getVhdInfoMock.Setup(m => m(@"x:\disks\storage-id-vm\sda.vhdx"))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 50 * 1024L * 1024 * 1024,
            }));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings, _getVhdInfoMock.Object);

        result.Should().BeLeft().Which.Message.Should().Be("Disk size is below minimum size of the virtual disk");
    }

    [Fact]
    public async Task PlanDriveStorageSettings_ExistingDiskWithConfiguredSizeLarger_UsesConfiguredSize()
    {
        var config = new CatletConfig
        {
            Drives = new[]
            {
                new CatletDriveConfig
                {
                    Type = CatletDriveType.VHD,
                    Name = "sda",
                    Size = 42,
                },
            },
        };

        _getVhdInfoMock.Setup(m => m(@"x:\disks\storage-id-vm\sda.vhdx"))
            .Returns(RightAsync<Error, Option<VhdInfo>>(new VhdInfo()
            {
                Size = 40 * 1024L * 1024 * 1024,
            }));

        var result = await VMDriveStorageSettings.PlanDriveStorageSettings(
            _vmHostAgentConfiguration, config, _storageSettings, _getVhdInfoMock.Object);

        result.Should().BeRight().Which.Should().SatisfyRespectively(
            dss =>
            {
                dss.Type.Should().Be(CatletDriveType.VHD);
                dss.ControllerNumber.Should().Be(0);
                dss.ControllerLocation.Should().Be(0);

                var settings = dss.Should().BeOfType<HardDiskDriveStorageSettings>().Subject;
                settings.AttachPath.Should().Be(@"x:\disks\storage-id-vm\sda.vhdx");
                settings.DiskSettings.SizeBytes.Should().Be(42 * 1024L * 1024 * 1024);
                settings.DiskSettings.SizeBytesCreate.Should().Be(42 * 1024L * 1024 * 1024);

                settings.DiskSettings.ParentSettings.Should().BeNone();
            });
    }

    private void AssertParent(Option<DiskStorageSettings> parentSettings)
    {
        var settings = parentSettings.Should().BeSome().Subject;
        settings.Path.Should().Be(@"x:\disks\genepool\testorg\testset\testtag\volumes");
        settings.Name.Should().Be("sda");
    }
}
