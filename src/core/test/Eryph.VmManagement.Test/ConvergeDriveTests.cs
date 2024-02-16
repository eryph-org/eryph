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
                Id = new Guid(),
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

                if (commandString.Contains("Get-VHD [x:\\disks\\abc\\sda.vhdx]", StringComparison.InvariantCultureIgnoreCase))
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
            vhdCommand?.ShouldBeCommand("Resize-VHD")
                .ShouldBeParam("Path", "x:\\disks\\abc\\sda.vhdx")
                .ShouldBeParam("SizeBytes", newSize*1024L*1024*1024);
        }

        [Fact]
        public async Task Converges_new_disk()
        {
            _fixture.Config.Drives = new[] { new CatletDriveConfig { Name = "sdb" } };
            _fixture.StorageSettings = _fixture.StorageSettings with
            {
                DefaultVhdPath = @"x:\disks\abc",
                StorageIdentifier = "abc",
                StorageNames = StorageNames.FromVmPath(@"x:\data\abc", _fixture.VmHostAgentConfiguration).Names
            };


            var vmData = _fixture.Engine.ToPsObject(new Data.Full.VirtualMachineInfo{
                Id = new Guid(),
                HardDrives = new []{new HardDiskDriveInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    ControllerLocation = 0,
                    ControllerNumber = 0,
                    ControllerType = ControllerType.SCSI,
                    Path = @"x:\disks\abc\sda.vhdx"
                }}});

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

                if (commandString.Contains("Test-Path [x:\\disks\\abc\\sdb.vhdx]"))
                    return new[] { _fixture.Engine.ToPsObject<object>(false) }.ToSeq();


                if (commandString.Contains("Get-VHD"))
                    return new[] { _fixture.Engine.ToPsObject<object>(new VhdInfo
                    {
                        Path = commandString.Contains("sda") ? @"x:\disks\abc\sda.vhdx": @"x:\disks\abc\sdb.vhdx",
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
            vhdCommand?.ShouldBeCommand("New-VHD")
                .ShouldBeParam("Path", @"x:\disks\abc\sdb.vhdx")
                .ShouldBeFlag("Dynamic")
                .ShouldBeParam("SizeBytes", 1073741824);

            attachCommand.Should().NotBeNull();
            attachCommand?.ShouldBeCommand("Add-VMHardDiskDrive")
                .ShouldBeParam("VM", vmData.PsObject)
                .ShouldBeParam("Path", @"x:\disks\abc\sdb.vhdx");
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
                Id = new Guid(),
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
                    return new[] { _fixture.Engine.ToPsObject<object>(vmData.Value) }.ToSeq();

                if (commandString.Contains(@"Test-Path [x:\disks\abc\sda.vhdx]"))
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

            vhdCommand.Should().NotBeNull();
            vhdCommand!.ShouldBeCommand("New-VHD")
                .ShouldBeParam("Path", @"x:\disks\abc\sda.vhdx")
                .ShouldBeParam("ParentPath", @"x:\disks\genepool\testorg\testset\testtag\volumes\sda.vhdx")
                .ShouldBeFlag("Differencing")
                .ShouldBeParam("SizeBytes", 1073741824);

            attachCommand.Should().NotBeNull();
            attachCommand?.ShouldBeCommand("Add-VMHardDiskDrive")
                .ShouldBeParam("VM", vmData.PsObject)
                .ShouldBeParam("Path", @"x:\disks\abc\sda.vhdx");
        }
    }
}
