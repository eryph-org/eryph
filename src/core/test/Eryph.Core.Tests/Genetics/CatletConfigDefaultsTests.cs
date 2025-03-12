using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class CatletConfigDefaultsTests
{
    [Fact]
    public void ApplyDefaultNetwork_NoNetworksConfigured_AppliesDefaultNetwork()
    {
        var config = new CatletConfig();

        var result = CatletConfigDefaults.ApplyDefaultNetwork(config);

        result.Networks.Should().SatisfyRespectively(
            n =>
            {
                n.Name.Should().Be(EryphConstants.DefaultNetworkName);
                n.AdapterName.Should().BeNull();
                n.SubnetV4.Should().BeNull();
                n.SubnetV6.Should().BeNull();
            });
    }

    [Fact]
    public void ApplyDefaultNetwork_NetworkConfigured_KeepsExistingNetwork()
    {
        var config = new CatletConfig()
        {
            Networks =
            [
                new CatletNetworkConfig
                {
                    Name = "network1",
                },
            ],
        };

        var result = CatletConfigDefaults.ApplyDefaultNetwork(config);

        result.Networks.Should().SatisfyRespectively(
            network =>
            {
                network.Name.Should().Be("network1");
                network.AdapterName.Should().BeNull();
            });
    }

    [Fact]
    public void ApplyDefaults_MissingData_AppliesDefaults()
    {
        var config = new CatletConfig()
        {
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

        var result = CatletConfigDefaults.ApplyDefaults(config);

        result.Name.Should().Be(EryphConstants.DefaultCatletName);
        
        result.Cpu.Should().NotBeNull();
        result.Cpu!.Count.Should().Be(EryphConstants.DefaultCatletCpuCount);

        result.Memory.Should().NotBeNull();
        result.Memory!.Startup.Should().Be(EryphConstants.DefaultCatletMemoryMb);
        result.Memory!.Minimum.Should().BeNull();
        result.Memory!.Maximum.Should().BeNull();

        result.Networks.Should().SatisfyRespectively(
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


    }

    [Fact]
    public void ApplyDefaults_NoMissingData_KeepsExistingData()
    {
        var config = new CatletConfig()
        {
            Name = "test-catlet",
            Cpu = new CatletCpuConfig
            {
                Count = 2,
            },
            Memory = new CatletMemoryConfig
            {
                Startup = 4096,
            },
            Networks =
            [
                new CatletNetworkConfig
                {
                    Name = "test-network",
                    AdapterName = "eth1000",
                },
            ],
        };

        var result = CatletConfigDefaults.ApplyDefaults(config);
        result.Name.Should().Be("test-catlet");

        result.Cpu.Should().NotBeNull();
        result.Cpu!.Count.Should().Be(2);

        result.Memory.Should().NotBeNull();
        result.Memory!.Startup.Should().Be(4096);
        result.Memory!.Minimum.Should().BeNull();
        result.Memory!.Maximum.Should().BeNull();

        result.Networks.Should().SatisfyRespectively(
            n =>
            {
                n.Name.Should().Be("test-network");
                n.AdapterName.Should().Be("eth1000");
                n.SubnetV4.Should().BeNull();
                n.SubnetV6.Should().BeNull();
            });
    }
}
