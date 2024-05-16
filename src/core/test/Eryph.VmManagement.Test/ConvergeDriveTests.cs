using Eryph.ConfigModel.Catlets;
using Eryph.Resources.Disks;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Storage;
using FluentAssertions;
using LanguageExt;
using Xunit;

namespace Eryph.VmManagement.Test
{
    public class ConvergeDriveTests : IClassFixture<ConvergeFixture>
    {
        private readonly ConvergeFixture _fixture;

        public ConvergeDriveTests(ConvergeFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData(null, 40, 40)]
        [InlineData(0, 40, 40)]
        [InlineData(40,30, 40)]
        [InlineData(50, 30, 50)]
        public async Task Converges_existing_disk(int? configSize, int currentSize, int newSize)
        {
            _fixture.Config.Drives = new[] { new CatletDriveConfig { Name = "sda" , Size = configSize} };
            _fixture.StorageSettings = _fixture.StorageSettings with
            {
                DefaultVhdPath = @"x:\disks\abc",
                StorageIdentifier = "abc",
                StorageNames = StorageNames.FromVmPath(@"x:\data\abc", _fixture.VmHostAgentConfiguration).Names
            };


            var vmData = _fixture.Engine.ToPsObject(new Data.Full.VirtualMachineInfo
            {
                Id = Guid.NewGuid(),
                HardDrives = new[]{new HardDiskDriveInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    ControllerLocation = 0,
                    ControllerNumber = 0,
                    ControllerType = ControllerType.SCSI,
                    Path = @"x:\disks\abc\sda.vhdx"
                }}
            });

            AssertCommand? vhdCommand = null;

            _fixture.Engine.RunCallback = command =>
            {
                if (command.ToString().Contains("CheckpointType"))
                {
                    command.ShouldBeCommand("Set-VM")
                        .ShouldBeParam("VM", vmData.PsObject)
                        .ShouldBeParam("CheckpointType");
                }

                if (command.ToString().Contains("Resize-VHD")) vhdCommand = command;

                return Unit.Default;
            };

            _fixture.Engine.GetObjectCallback = (type, command) =>
            {
                var commandString = command.ToString();
                if (commandString.Contains("Get-VM"))
                    return new[] { _fixture.Engine.ToPsObject<object>(vmData.Value) }.ToSeq();

                if (commandString.Contains("Test-Path [x:\\disks\\abc\\sda.vhdx]"))
                    return new [] { _fixture.Engine.ToPsObject<object>(true) }.ToSeq();

                if (commandString.Contains("Get-VHD [x:\\disks\\abc\\sda.vhdx]", StringComparison.OrdinalIgnoreCase))
                    return new[] { _fixture.Engine.ToPsObject<object>(new VhdInfo
                    {
                        Path = @"x:\disks\abc\sda.vhdx",
                        Size = currentSize*1024L*1024*1024
                    }) }.ToSeq();

                return new PowershellFailure { Message = $"unknown command: {commandString}" };
            };

            var convergeTask = new ConvergeDrives(_fixture.Context);
            _ = (await convergeTask.Converge(vmData)).IfLeft(l => l.Throw());

            if (configSize == null || configSize == 0 || configSize == currentSize)
            {
                vhdCommand.Should().BeNull();
                return;
            }

            vhdCommand.Should().NotBeNull();
            vhdCommand!.ShouldBeCommand("Resize-VHD")
                .ShouldBeParam("Path", "x:\\disks\\abc\\sda.vhdx")
                .ShouldBeParam("SizeBytes", newSize*1024L*1024*1024);
        }

        [Theory]
        [InlineData(CatletDriveType.VHD, ".vhdx")]
        [InlineData(CatletDriveType.SharedVHD, ".vhdx")]
        [InlineData(CatletDriveType.VHDSet, ".vhds")]
        public async Task Converges_new_disk(
            CatletDriveType driveType,
            string extension)
        {
            _fixture.Config.Drives = new[] { new CatletDriveConfig { Name = "sdb", Type = driveType} };
            _fixture.StorageSettings = _fixture.StorageSettings with
            {
                DefaultVhdPath = @"x:\disks\abc",
                StorageIdentifier = "abc",
                StorageNames = StorageNames.FromVmPath(@"x:\data\abc", _fixture.VmHostAgentConfiguration).Names
            };


            var vmData = _fixture.Engine.ToPsObject(new Data.Full.VirtualMachineInfo
            {
                Id = Guid.NewGuid(),
                HardDrives = new []
                {
                    new HardDiskDriveInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        ControllerLocation = 0,
                        ControllerNumber = 0,
                        ControllerType = ControllerType.SCSI,
                        Path = @"x:\disks\abc\sda.vhdx",
                    },
                },
            });

            AssertCommand? vhdCommand = null;
            AssertCommand? attachCommand = null;

            _fixture.Engine.RunCallback = command =>
            {
                if (command.ToString().Contains("CheckpointType"))
                {
                    command.ShouldBeCommand("Set-VM")
                        .ShouldBeParam("VM", vmData.PsObject)
                        .ShouldBeParam("CheckpointType");
                }

                if (command.ToString().Contains("New-VHD")) vhdCommand = command;

                return Unit.Default;
            };

            _fixture.Engine.GetObjectCallback = (type, command) =>
            {
                var commandString = command.ToString();
                if (commandString.Contains("Get-VM"))
                    return new []{ _fixture.Engine.ToPsObject<object>(vmData.Value)}.ToSeq();

                if (commandString.Contains($@"Test-Path [x:\disks\abc\sdb{extension}]"))
                    return new[] { _fixture.Engine.ToPsObject<object>(false) }.ToSeq();


                if (commandString.Contains("Get-VHD"))
                    return new[] { _fixture.Engine.ToPsObject<object>(new VhdInfo
                    {
                        Path = commandString.Contains("sda") ? @"x:\disks\abc\sda.vhdx": $@"x:\disks\abc\sdb{extension}",
                        Size = 1073741824
                    }) }.ToSeq();

                if (command.ToString().Contains("Add-VMHardDiskDrive"))
                {
                    attachCommand = command;
                    var res = new HardDiskDriveInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        ControllerLocation = 0,
                        ControllerNumber = 0,
                        ControllerType = ControllerType.SCSI,
                        Path = @"x:\disks\abc\sda.vhdx"
                    };

                    return new[] { _fixture.Engine.ToPsObject<object>(res)}.ToSeq();
                }


                return new PowershellFailure { Message = $"unknown command: {commandString}" };
            };

            var convergeTask = new ConvergeDrives(_fixture.Context);
            _ = (await convergeTask.Converge(vmData)).IfLeft(l=>l.Throw());

            vhdCommand.Should().NotBeNull();
            vhdCommand!.ShouldBeCommand("New-VHD")
                .ShouldBeParam("Path", $@"x:\disks\abc\sdb{extension}")
                .ShouldBeFlag("Dynamic")
                .ShouldBeParam("SizeBytes", 1073741824);

            attachCommand.Should().NotBeNull();
            attachCommand!.ShouldBeCommand("Add-VMHardDiskDrive")
                .ShouldBeParam("VM", vmData.PsObject)
                .ShouldBeParam("Path", $@"x:\disks\abc\sdb{extension}");

            if (driveType is CatletDriveType.SharedVHD)
            {
                attachCommand!.ToString().Should().Contain("SupportPersistentReservations");
            }
            else
            {
                attachCommand!.ToString().Should().NotContain("SupportPersistentReservations");
            }
        }

        [Fact]
        public async Task Converges_new_disk_with_genepool_parent()
        {
            _fixture.Config.Drives = new[]
            {
                new CatletDriveConfig { Name = "sda", Source = "gene:testorg/testset/testtag:sda" }
            };

            _fixture.StorageSettings = _fixture.StorageSettings with
            {
                DefaultVhdPath = @"x:\disks\abc",
                StorageIdentifier = "abc",
                StorageNames = StorageNames.FromVmPath(@"x:\data\abc", _fixture.VmHostAgentConfiguration).Names
            };

            var vmData = _fixture.Engine.ToPsObject(new Data.Full.VirtualMachineInfo
            {
                Id = Guid.NewGuid(),
            });

            AssertCommand? newVhdCommand = null;
            AssertCommand? setVhdCommand = null;
            AssertCommand? attachCommand = null;

            _fixture.Engine.RunCallback = command =>
            {
                if (command.ToString().Contains("CheckpointType"))
                {
                    command.ShouldBeCommand("Set-VM")
                        .ShouldBeParam("VM", vmData.PsObject)
                        .ShouldBeParam("CheckpointType");
                }

                if (command.ToString().Contains("New-VHD")) newVhdCommand = command;

                if (command.ToString().Contains("Set-VHD")) setVhdCommand = command;

                return Unit.Default;
            };

            _fixture.Engine.GetObjectCallback = (type, command) =>
            {
                var commandString = command.ToString();
                if (commandString.Contains("Get-VM"))
                    return new[] { _fixture.Engine.ToPsObject<object>(vmData.Value) }.ToSeq();

                if (commandString.Contains(@"Test-Path [x:\disks\abc\sda_g1.vhdx]"))
                    return new[] { _fixture.Engine.ToPsObject<object>(false) }.ToSeq();


                if (commandString.Contains("Get-VHD"))
                    return new[] { _fixture.Engine.ToPsObject<object>(new VhdInfo
                    {
                        Path =  @"x:\disks\abc\sda.vhdx",
                        Size = 1073741824
                    }) }.ToSeq();

                if (command.ToString().Contains("Add-VMHardDiskDrive"))
                {
                    attachCommand = command;
                    var res = new HardDiskDriveInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        ControllerLocation = 0,
                        ControllerNumber = 0,
                        ControllerType = ControllerType.SCSI,
                        Path = @"x:\disks\abc\sda.vhdx"
                    };

                    return new[] { _fixture.Engine.ToPsObject<object>(res) }.ToSeq();
                }

                return new PowershellFailure { Message = $"unknown command: {commandString}" };
            };

            var convergeTask = new ConvergeDrives(_fixture.Context);
            _ = (await convergeTask.Converge(vmData)).IfLeft(l => l.Throw());

            newVhdCommand.Should().NotBeNull();
            newVhdCommand!.ShouldBeCommand("New-VHD")
                .ShouldBeParam("Path", @"x:\disks\abc\sda_g1.vhdx")
                .ShouldBeParam("ParentPath", @"x:\disks\genepool\testorg\testset\testtag\volumes\sda.vhdx")
                .ShouldBeFlag("Differencing")
                .ShouldBeParam("SizeBytes", 1073741824);

            setVhdCommand.Should().NotBeNull();
            setVhdCommand!.ShouldBeCommand("Set-VHD").
                ShouldBeParam("Path", @"x:\disks\abc\sda_g1.vhdx")
                .ShouldBeFlag("ResetDiskIdentifier")
                .ShouldBeFlag("Force");

            attachCommand.Should().NotBeNull();
            attachCommand!.ShouldBeCommand("Add-VMHardDiskDrive")
                .ShouldBeParam("VM", vmData.PsObject)
                .ShouldBeParam("Path", @"x:\disks\abc\sda_g1.vhdx");
        }

        [Theory]
        [InlineData(CatletDriveType.SharedVHD)]
        [InlineData(CatletDriveType.VHDSet)]
        public async Task Converges_new_set_or_shared_disk_with_genepool_parent(CatletDriveType driveType)
        {
            _fixture.Config.Drives = new[]
            {
                new CatletDriveConfig
                {
                    Name = "sda", Source = "gene:testorg/testset/testtag:sda",
                    Type =driveType
                }
            };
            _fixture.StorageSettings = _fixture.StorageSettings with
            {
                DefaultVhdPath = @"x:\disks\abc",
                StorageIdentifier = "abc",
                StorageNames = StorageNames.FromVmPath(@"x:\data\abc", _fixture.VmHostAgentConfiguration).Names
            };

            var vmData = _fixture.Engine.ToPsObject(new Data.Full.VirtualMachineInfo
            {
                Id = Guid.NewGuid(),
            });

            AssertCommand? convertVhdCommand = null;
            AssertCommand? setVhdCommand = null;
            AssertCommand? copyCommand = null;
            AssertCommand? attachCommand = null;
            var diskName = driveType == CatletDriveType.SharedVHD ? "sda.vhdx" : "sda.vhds";

            _fixture.Engine.RunCallback = command =>
            {
                if (command.ToString().Contains("CheckpointType"))
                {
                    command.ShouldBeCommand("Set-VM")
                        .ShouldBeParam("VM", vmData.PsObject)
                        .ShouldBeParam("CheckpointType");
                }

                if (command.ToString().Contains("Convert-VHD")) convertVhdCommand = command;
                if (command.ToString().Contains("Set-VHD")) setVhdCommand = command;
                if (command.ToString().Contains("Copy-Item")) copyCommand = command;

                return Unit.Default;
            };

            _fixture.Engine.GetObjectCallback = (type, command) =>
            {
                var commandString = command.ToString();
                if (commandString.Contains("Get-VM"))
                    return new[] { _fixture.Engine.ToPsObject<object>(vmData.Value) }.ToSeq();

                if (commandString.Contains(@$"Test-Path [x:\disks\abc\{diskName}]"))
                    return new[] { _fixture.Engine.ToPsObject<object>(false) }.ToSeq();


                if (commandString.Contains("Get-VHD"))
                    return new[] { _fixture.Engine.ToPsObject<object>(new VhdInfo
                    {
                        Path =  @$"x:\disks\abc\{diskName}",
                        Size = 1073741824
                    }) }.ToSeq();


                if (command.ToString().Contains("Add-VMHardDiskDrive"))
                {
                    attachCommand = command;
                    var res = new HardDiskDriveInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        ControllerLocation = 0,
                        ControllerNumber = 0,
                        ControllerType = ControllerType.SCSI,
                        Path = $@"x:\disks\abc\{diskName}"
                    };

                    return new[] { _fixture.Engine.ToPsObject<object>(res) }.ToSeq();
                }

                return new PowershellFailure { Message = $"unknown command: {commandString}" };
            };

            var convergeTask = new ConvergeDrives(_fixture.Context);
            _ = (await convergeTask.Converge(vmData)).IfLeft(l => l.Throw());


            copyCommand.Should().NotBeNull();
            copyCommand!.ShouldBeCommand("Copy-Item")
                .ShouldBeArgument(@"x:\disks\genepool\testorg\testset\testtag\volumes\sda.vhdx")
                .ShouldBeArgument(@"x:\disks\abc\sda.vhdx");

            setVhdCommand.Should().NotBeNull();
            setVhdCommand!.ShouldBeCommand("Set-VHD").
                ShouldBeParam("Path", @"x:\disks\abc\sda.vhdx")
                .ShouldBeFlag("ResetDiskIdentifier")
                .ShouldBeFlag("Force");

            if (driveType == CatletDriveType.SharedVHD)
            {
                convertVhdCommand.Should().BeNull();
            }
            else
            {
                convertVhdCommand.Should().NotBeNull();
                convertVhdCommand!.ShouldBeCommand("Convert-VHD")
                    .ShouldBeArgument(@"x:\disks\abc\sda.vhdx")
                    .ShouldBeArgument(@"x:\disks\abc\sda.vhds");
            }

            attachCommand.Should().NotBeNull();

            if (driveType == CatletDriveType.SharedVHD)
            {
                attachCommand!.ShouldBeCommand("Add-VMHardDiskDrive")
                    .ShouldBeParam("VM", vmData.PsObject)
                    .ShouldBeParam("Path", $@"x:\disks\abc\{diskName}")
                    .ShouldBeFlag("SupportPersistentReservations");
            }
            else
            {
                attachCommand!.ShouldBeCommand("Add-VMHardDiskDrive")
                    .ShouldBeParam("VM", vmData.PsObject)
                    .ShouldBeParam("Path", $@"x:\disks\abc\{diskName}");
            }
        }
    }
}
