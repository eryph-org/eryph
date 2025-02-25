using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Network;

namespace Eryph.Core.Tests.Network;

public class NetworkProvidersConfigValidationsTests
{
    [Fact]
    public void ValidateNetworkProvidersConfiguration_DefaultConfig_ReturnsSuccess()
    {
        var config = NetworkProvidersConfigYamlSerializer.Deserialize(
            NetworkProvidersConfiguration.DefaultConfig);

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);
        
        result.Should().BeSuccess();
    }

    [Fact]
    public void ValidateNetworkProvidersConfiguration_NoProviders_ReturnsError()
    {
        var config = new NetworkProvidersConfiguration();

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders");
                issue.Message.Should().Be("The list must have 1 or more entries.");
            });
    }

    [Fact]
    public void ValidateNetworkProvidersConfig_BridgeIsNamedLikeIntegrationBridge_ReturnsError()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-int",
                    Subnets = [ArrangeDefaultSubnet()],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].BridgeName");
                issue.Message.Should().Be("The bridge name 'br-int' is reserved.");
            });
    }

    [Fact]
    public void ValidateNetworkProvidersConfig_DuplicateProviderNames_ReturnsError()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-pif",
                    Subnets = [ArrangeDefaultSubnet()],
                },
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-pif-2",
                    Subnets = [ArrangeDefaultSubnet()],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders");
                issue.Message.Should().Be("The network provider name 'default' is not unique.");
            });
    }

    [Fact]
    public void ValidateNetworkProvidersConfig_DuplicateBridgeNames_ReturnsError()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-nat",
                    Subnets = [ArrangeDefaultSubnet()],
                },
                new NetworkProvider
                {
                    Name = "second-provider",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-nat",
                    Subnets = [ArrangeDefaultSubnet()],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders");
                issue.Message.Should().Be("The bridge name 'br-nat' is not unique.");
            });
    }

    [Fact]
    public void ValidateNetworkProvidersConfig_DuplicateAdapterNames_ReturnsError()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-pif",
                    Adapters = ["test-adapter-1"],
                    Subnets = [ArrangeDefaultSubnet()],
                },
                new NetworkProvider
                {
                    Name = "second-provider",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-pif-2",
                    Adapters = ["test-adapter-1"],
                    Subnets = [ArrangeDefaultSubnet()],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders");
                issue.Message.Should().Be("The adapter 'test-adapter-1' is not unique.");
            });
    }

    [Fact]
    public void ValidateNetworkProvidersConfig_FlatProviderWithInvalidValues_ReturnsError()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.Flat,
                    BridgeName = "br-pif",
                    SwitchName = "test-switch",
                    Adapters = ["test-adapter-1"],
                    Vlan = 42,
                    BridgeOptions = new NetworkProviderBridgeOptions
                    {
                        BridgeVlan = 42,
                    },
                    Subnets = [ArrangeDefaultSubnet()],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].BridgeName");
                issue.Message.Should().Be("The flat network provider does not use the bridge name.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].BridgeOptions");
                issue.Message.Should().Be("The flat network provider does not support bridge options.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Adapters");
                issue.Message.Should().Be("The flat network provider does not use adapters.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Vlan");
                issue.Message.Should().Be("The flat network provider does not support the configuration of a VLAN.");
            });
    }

    [Fact]
    public void ValidateNetworkProvidersConfig_NatOverlayProviderWithInvalidValues_ReturnsError()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br_pif",
                    SwitchName = "test-switch",
                    Adapters = ["test-adapter-1"],
                    Vlan = 42,
                    BridgeOptions = new NetworkProviderBridgeOptions
                    {
                        BridgeVlan = 42,
                    },
                    Subnets = [ArrangeDefaultSubnet()],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].BridgeName");
                issue.Message.Should().Match("The bridge name contains invalid characters.*");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].SwitchName");
                issue.Message.Should().Be("The NAT overlay network provider does not support custom switch names.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].BridgeOptions");
                issue.Message.Should().Be("The NAT overlay network provider does not support bridge options.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Adapters");
                issue.Message.Should().Be("The NAT overlay network provider does not use adapters.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Vlan");
                issue.Message.Should().Be("The NAT overlay network provider does not support the configuration of a VLAN.");
            });
    }

    [Fact]
    public void ValidateNetworkProvidersConfig_OverlayProviderWithInvalidValues_ReturnsError()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br_pif",
                    SwitchName = "test-switch",
                    Adapters = ["test-adapter-1"],
                    Vlan = 8042,
                    BridgeOptions = new NetworkProviderBridgeOptions
                    {
                        BridgeVlan = 8042,
                    },
                    Subnets = [ArrangeDefaultSubnet()],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].BridgeName");
                issue.Message.Should().Match("The bridge name contains invalid characters.*");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].SwitchName");
                issue.Message.Should().Be("The overlay network provider does not support custom switch names.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].BridgeOptions.BridgeVlan");
                issue.Message.Should().Be("The VLAN tag must be less than 4096.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Vlan");
                issue.Message.Should().Be("The VLAN tag must be less than 4096.");
            });
    }

    private static NetworkProviderSubnet ArrangeDefaultSubnet() =>
        new()
        {
            Name = EryphConstants.DefaultSubnetName,
            Gateway = "10.249.0.1",
            Network = "10.249.0.0/22",
            IpPools =
            [
                new NetworkProviderIpPool
                {
                    Name = EryphConstants.DefaultSubnetName,
                    FirstIp = "10.249.0.50",
                    NextIp = "10.249.0.60",
                    LastIp = "10.249.0.100"
                },
            ],
        };
}
