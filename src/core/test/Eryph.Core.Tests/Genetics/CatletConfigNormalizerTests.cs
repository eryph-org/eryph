using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class CatletConfigNormalizerTests
{
    [Fact]
    public void Minimize_WithEmptyValues_RemovesEmptyValues()
    {
        var config = new CatletConfig
        {
            Cpu = new CatletCpuConfig(),
            Memory = new CatletMemoryConfig(),
            Capabilities = [],
            Drives = [],
            NetworkAdapters = [],
            Networks = [],
            Fodder = [],
            Variables = [],
        };

        var result = CatletConfigNormalizer.Minimize(config);

        result.Cpu.Should().BeNull();
        result.Memory.Should().BeNull();
        result.Capabilities.Should().BeNull();
        result.Drives.Should().BeNull();
        result.NetworkAdapters.Should().BeNull();
        result.Networks.Should().BeNull();
        result.Fodder.Should().BeNull();
        result.Variables.Should().BeNull();
    }

    [Fact]
    public void Minimize_WithEmptyVariablesInFodder_RemovesEmptyVariables()
    {
        var config = new CatletConfig
        {
            Fodder =
            [
                new FodderConfig
                {
                    Variables = [],
                },
            ],
        };

        var result = CatletConfigNormalizer.Minimize(config);

        result.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Variables.Should().BeNull());
    }

    [Fact]
    public void Minimize_NameAndHostnameAreIdentical_RemovesHostname()
    {
        var config = new CatletConfig
        {
            Name = "test-catlet",
            Hostname = "test-catlet",
        };

        var result = CatletConfigNormalizer.Minimize(config);

        result.Hostname.Should().BeNull();
    }

    [Fact]
    public void Normalize_WithoutDefaultValues_AddsDefaultValues()
    {
        var config = new CatletConfig()
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Name = "sda",
                },
            ],
            Networks =
            [
                new CatletNetworkConfig
                {
                    Name = "network1",
                },
                new CatletNetworkConfig
                {
                    Name = "network2",
                },
            ],
        };

        var result = CatletConfigNormalizer.Normalize(config);

        var normalizedConfig = result.Should().BeSuccess().Subject;

        normalizedConfig.Project.Should().Be(EryphConstants.DefaultProjectName);
        normalizedConfig.Environment.Should().Be(EryphConstants.DefaultEnvironmentName);
        normalizedConfig.Store.Should().Be(EryphConstants.DefaultDataStoreName);

        normalizedConfig.Drives.Should().SatisfyRespectively(
            drive =>
            {
                drive.Type.Should().Be(CatletDriveType.Vhd);
                drive.Name.Should().Be("sda");
                drive.Store.Should().Be(EryphConstants.DefaultDataStoreName);
            });

        normalizedConfig.Networks.Should().SatisfyRespectively(
            network =>
            {
                network.Name.Should().Be("network1");
                network.AdapterName.Should().Be("eth0");
            },
            network =>
            {
                network.Name.Should().Be("network2");
                network.AdapterName.Should().Be("eth1");
            });

        normalizedConfig.NetworkAdapters.Should().SatisfyRespectively(
            adapter => adapter.Name.Should().Be("eth0"),
            adapter => adapter.Name.Should().Be("eth1"));
    }

    [Fact]
    public void Normalize_IncorrectCapitalization_NormalizesCapitalization()
    {
        var config = new CatletConfig()
        {
            Name = "Test-Catlet",
            Parent = "Acme/Acme-OS",
            Project = "Test-Project",
            Environment = "Test-Environment",
            Store = "Test-Store",
            Location = "Test-Location",
            Drives =
            [
                new CatletDriveConfig
                {
                    Name = "Test-Drive",
                    Store = "Test-Drive-Store",
                    Location = "Test-Drive-Location",
                    Source = "gene:Acme/Acme-OS:SDA",
                },
            ],
            Networks =
            [
                new CatletNetworkConfig
                {
                    Name = "Test-Network",
                    AdapterName = "Test-Adapter",
                    SubnetV4 = new CatletSubnetConfig
                    {
                        Name = "Test-Subnet",
                        IpPool = "Test-Pool",
                    },
                    SubnetV6 = new CatletSubnetConfig
                    {
                        Name = "Test-Subnet",
                        IpPool = "Test-Pool",
                    }
                },
            ],
            NetworkAdapters =
            [
                new CatletNetworkAdapterConfig
                {
                    Name = "Test-Adapter",
                },
            ],
            Variables =
            [
                new VariableConfig
                {
                    Name = "Test_Variable",
                },
            ],
            Fodder =
            [
                new FodderConfig
                {
                    Name = "Test-Fodder",
                    Variables =
                    [
                        new VariableConfig
                        {
                            Name = "Test_Variable",
                        },
                    ],
                },
            ],
        };

        var result = CatletConfigNormalizer.Normalize(config);

        var normalizedConfig = result.Should().BeSuccess().Subject;

        normalizedConfig.Name.Should().Be("test-catlet");
        normalizedConfig.Hostname.Should().Be("test-catlet");
        normalizedConfig.Parent.Should().Be("acme/acme-os/latest");
        normalizedConfig.Project.Should().Be("test-project");
        normalizedConfig.Environment.Should().Be("test-environment");
        normalizedConfig.Store.Should().Be("test-store");
        normalizedConfig.Location.Should().Be("test-location");

        normalizedConfig.Drives.Should().SatisfyRespectively(
            drive =>
            {
                drive.Name.Should().Be("test-drive");
                drive.Store.Should().Be("test-drive-store");
                drive.Location.Should().Be("test-drive-location");
                drive.Source.Should().Be("gene:acme/acme-os/latest:sda");
            });

        normalizedConfig.Networks.Should().SatisfyRespectively(
            network =>
            {
                network.Name.Should().Be("test-network");
                network.AdapterName.Should().Be("test-adapter");
                
                network.SubnetV4.Should().NotBeNull();
                network.SubnetV4!.Name.Should().Be("test-subnet");
                network.SubnetV4.IpPool.Should().Be("test-pool");
                
                network.SubnetV6.Should().NotBeNull();
                network.SubnetV6!.Name.Should().Be("test-subnet");
                network.SubnetV6.IpPool.Should().Be("test-pool");
            });

        normalizedConfig.NetworkAdapters.Should().SatisfyRespectively(
            adapter => adapter.Name.Should().Be("test-adapter"));

        normalizedConfig.Variables.Should().SatisfyRespectively(
            variable =>
            {
                variable.Type.Should().Be(VariableType.String);
                variable.Name.Should().Be("test_variable");
            });

        normalizedConfig.Fodder.Should().SatisfyRespectively(
            fodder =>
            {
                fodder.Name.Should().Be("test-fodder");
                fodder.Variables.Should().SatisfyRespectively(
                    variable =>
                    {
                        variable.Type.Should().Be(VariableType.String);
                        variable.Name.Should().Be("test_variable");
                    });
            });
    }
}
