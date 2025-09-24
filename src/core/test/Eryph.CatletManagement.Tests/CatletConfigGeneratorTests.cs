using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.StateDb.Model;
using FluentAssertions;

using static LanguageExt.Prelude;

namespace Eryph.CatletManagement.Tests;

public class CatletConfigGeneratorTests
{
    [Fact]
    public void Generate_ComplexCatlet_ReturnsConfig()
    {
        var originalConfig = new CatletConfig
        {
            Parent = "acme/acme-os/1.0",
            Hostname = "test-hostname",
        };

        var catlet = new Catlet
        {
            Name = "test-catlet",
            Project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "test-project",
            },
            Environment = "test-environment",
            DataStore = "test-vm-store",
            StorageIdentifier = "test-location",
            CpuCount = 3,
            StartupMemory = 1024 * 1024 * 1024L,
            MinimumMemory = 512 * 1024 * 1024L,
            MaximumMemory = 2048 * 1024 * 1024L,
            SecureBootTemplate = "MicrosoftUEFICertificateAuthority",
            Features = new HashSet<CatletFeature>(
            [
                CatletFeature.DynamicMemory,
                CatletFeature.NestedVirtualization,
                CatletFeature.SecureBoot,
                CatletFeature.Tpm,
            ]),
            Drives =
            [
                new CatletDrive
                {
                    Id = @"Microsoft:853F42A8-62CC-437F-832B-BAA91727B586\4ABB4F48-A547-4152-AEC3-4F4A7B0E111C\0\0\D",
                    Type = CatletDriveType.Vhd,
                    AttachedDisk = new VirtualDisk
                    {
                        Id = Guid.NewGuid(),
                        Name = "sda",
                        Environment = "test-environment",
                        DataStore = "test-disk-store",
                        StorageIdentifier = "test-disk-location",
                        SizeBytes = 100 *1024 * 1024 * 1024L,
                        Parent = new VirtualDisk
                        {
                            Id = Guid.NewGuid(),
                            Name = "sda",
                            Environment = EryphConstants.DefaultEnvironmentName,
                            DataStore = EryphConstants.DefaultDataStoreName,
                            GeneSet = "acme/acme-os/1.0",
                            GeneName = "sda",
                            SizeBytes = 100 *1024 * 1024 * 1024L,
                        }
                    }
                },
                new CatletDrive
                {
                    Id = @"Microsoft:853F42A8-62CC-437F-832B-BAA91727B586\4ABB4F48-A547-4152-AEC3-4F4A7B0E111C\0\1\D"
                }
            ],
            NetworkAdapters =
            [
                new CatletNetworkAdapter
                {
                    Id =  @"Microsoft:853F42A8-62CC-437F-832B-BAA91727B586\7C18C0B3-4331-45DD-BBC5-19BB4330E15A",
                    Name = "eth0",
                    MacAddress = "02:04:06:08:0a:0c",
                    SwitchName = EryphConstants.OverlaySwitchName,
                }
            ],
        };

        var networkPorts = Seq1(new CatletNetworkPort
        {
            Name = "test-catlet-port",
            MacAddress = "02:04:06:08:0a:0c",
            Network = new VirtualNetwork
            {
                Name = "test-network",
                Environment = "test-environment",
                NetworkProvider = EryphConstants.DefaultProviderName,
            },
            IpAssignments =
            [
                new IpPoolAssignment
                {
                    Subnet = new VirtualNetworkSubnet
                    {
                        Name = "test-subnet",
                    },
                    Pool = new IpPool
                    {
                        Name = "test-pool",
                    },
                },
            ]
        });

        var result = CatletConfigGenerator.Generate(catlet, networkPorts, originalConfig);

        result.Name.Should().Be("test-catlet");
        result.Project.Should().Be("test-project");
        result.Environment.Should().Be("test-environment");
        result.Store.Should().Be("test-vm-store");
        result.Location.Should().Be("test-location");
        result.Parent.Should().Be("acme/acme-os/1.0");
        result.Hostname.Should().Be("test-hostname");

        result.Cpu.Should().NotBeNull();
        result.Cpu!.Count.Should().Be(3);

        result.Memory.Should().NotBeNull();
        result.Memory!.Startup.Should().Be(1024);
        result.Memory.Minimum.Should().Be(512);
        result.Memory.Maximum.Should().Be(2048);

        result.Capabilities.Should().SatisfyRespectively(
            capability => capability.Name.Should().Be(EryphConstants.Capabilities.DynamicMemory),
            capability => capability.Name.Should().Be(EryphConstants.Capabilities.NestedVirtualization),
            capability =>
            {
                capability.Name.Should().Be(EryphConstants.Capabilities.SecureBoot);
                capability.Details.Should().Equal("template:MicrosoftUEFICertificateAuthority");
            },
            capability => capability.Name.Should().Be(EryphConstants.Capabilities.Tpm));

        result.Drives.Should().SatisfyRespectively(
            drive =>
            {
                drive.Name.Should().Be("sda");
                drive.Source.Should().Be("gene:acme/acme-os/1.0:sda");
                drive.Store.Should().Be("test-disk-store");
                drive.Location.Should().Be("test-disk-location");
                // The drive size should not be populated when the size of the disk
                // matches the size of the gene pool disk.
                drive.Size.Should().BeNull();
            },
            drive =>
            {
                drive.Name.Should().Be("sdb");
                drive.Source.Should().BeNull();
            });

        result.NetworkAdapters.Should().SatisfyRespectively(adapter =>
        {
            adapter.Name.Should().Be("eth0");
            adapter.MacAddress.Should().Be("02:04:06:08:0a:0c");
        });

        result.Networks.Should().SatisfyRespectively(network =>
        {
            network.Name.Should().Be("test-network");
            network.AdapterName.Should().Be("eth0");
            network.SubnetV4.Should().NotBeNull();
            network.SubnetV4!.Name.Should().Be("test-subnet");
            network.SubnetV4!.IpPool.Should().Be("test-pool");
            network.SubnetV6.Should().BeNull();
        });
    }

    [Fact]
    public void Generate_CatletWithDynamicMemoryDisabled_ReturnsConfigWithMinAndMaxMemory()
    {
        var catlet = new Catlet
        {
            Name = "test-catlet",
            Project = new Project
            {
                Id = EryphConstants.DefaultProjectId,
                Name = EryphConstants.DefaultProjectName,
            },
            Environment = EryphConstants.DefaultEnvironmentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            StartupMemory = 1024 * 1024 * 1024L,
            MinimumMemory = 512 * 1024 * 1024L,
            MaximumMemory = 2048 * 1024 * 1024L,
        };

        var result = CatletConfigGenerator.Generate(catlet, Empty, new CatletConfig());

        result.Memory.Should().NotBeNull();
        result.Memory!.Startup.Should().Be(1024);
        result.Memory.Minimum.Should().BeNull();
        result.Memory.Maximum.Should().BeNull();
    }
}
