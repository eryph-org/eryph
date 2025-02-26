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

    [Theory]
    [InlineData(NetworkProviderType.Flat)]
    [InlineData(NetworkProviderType.NatOverlay)]
    [InlineData(NetworkProviderType.Overlay)]
    public void ValidateNetworkProviderConfig_SubnetWithDenormalizedNetwork_ReturnsError(
        NetworkProviderType providerType)
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = EryphConstants.DefaultProviderName,
                    Type = providerType,
                    BridgeName = providerType is NetworkProviderType.Flat ? null : "br-test",
                    SwitchName = providerType is NetworkProviderType.Flat ? "test-switch" : null,
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = EryphConstants.DefaultSubnetName,
                            Gateway = "10.249.249.1",
                            Network = "10.249.249.0/22",
                            IpPools =
                            [
                                new NetworkProviderIpPool
                                {
                                    Name = EryphConstants.DefaultIpPoolName,
                                    FirstIp = "10.249.249.50",
                                    NextIp = "10.249.249.60",
                                    LastIp = "10.249.249.100"
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Subnets[0].Network");
                issue.Message.Should().Match("The normalized IP network '10.249.248.0/22' does not match the specified network '10.249.249.0/22'.*");
            });
    }

    [Theory]
    [InlineData(NetworkProviderType.Flat)]
    [InlineData(NetworkProviderType.NatOverlay)]
    [InlineData(NetworkProviderType.Overlay)]
    public void ValidateNetworkProviderConfig_SubnetWithMismatchedIpAddresses_ReturnsError(
        NetworkProviderType providerType)
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = EryphConstants.DefaultProviderName,
                    Type = providerType,
                    BridgeName = providerType is NetworkProviderType.Flat ? null : "br-test",
                    SwitchName = providerType is NetworkProviderType.Flat ? "test-switch" : null,
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = EryphConstants.DefaultSubnetName,
                            Gateway = "10.249.0.1",
                            Network = "10.249.248.0/22",
                            IpPools =
                            [
                                new NetworkProviderIpPool
                                {
                                    Name = EryphConstants.DefaultIpPoolName,
                                    FirstIp = "10.249.0.50",
                                    NextIp = "10.249.0.60",
                                    LastIp = "10.249.0.100"
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Subnets[0].Gateway");
                issue.Message.Should().Be("The IP address '10.249.0.1' is not part of the provider's network '10.249.248.0/22'.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Subnets[0].IpPools[0].FirstIp");
                issue.Message.Should().Be("The IP address '10.249.0.50' is not part of the provider's network '10.249.248.0/22'.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Subnets[0].IpPools[0].NextIp");
                issue.Message.Should().Be("The IP address '10.249.0.60' is not part of the provider's network '10.249.248.0/22'.");
            },
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Subnets[0].IpPools[0].LastIp");
                issue.Message.Should().Be("The IP address '10.249.0.100' is not part of the provider's network '10.249.248.0/22'.");
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

    [Theory]
    [InlineData("other-subnet", "default")]
    [InlineData("default", "other-pool")]
    public void ValidateNetworkProvidersConfig_NatOverlayProviderWithInvalidSubnet_ReturnsError(
        string subnetName,
        string ipPoolName)
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-nat",
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = subnetName,
                            Gateway = "10.249.0.1",
                            Network = "10.249.0.0/22",
                            IpPools =
                            [
                                new NetworkProviderIpPool
                                {
                                    Name = ipPoolName,
                                    FirstIp = "10.249.0.50",
                                    NextIp = "10.249.0.60",
                                    LastIp = "10.249.0.100"
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders[0].Subnets");
                issue.Message.Should().Be("The NAT overlay provider must contain only the default subnet with the default IP pool.");
            });
    }

    [Fact]
    public void ValidateNetworkProvidersConfig_OverlappingNatNetworks_ReturnsError()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-nat",
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = EryphConstants.DefaultSubnetName,
                            Gateway = "10.249.0.1",
                            Network = "10.249.0.0/22",
                            IpPools =
                            [
                                new NetworkProviderIpPool
                                {
                                    Name = EryphConstants.DefaultIpPoolName,
                                    FirstIp = "10.249.0.50",
                                    NextIp = "10.249.0.60",
                                    LastIp = "10.249.0.100"
                                },
                            ],
                        },
                    ],
                },
                new NetworkProvider
                {
                    Name = "second-nat-provider",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-nat-2",
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = EryphConstants.DefaultSubnetName,
                            Gateway = "10.249.1.1",
                            Network = "10.249.1.0/24",
                            IpPools =
                            [
                                new NetworkProviderIpPool
                                {
                                    Name = EryphConstants.DefaultIpPoolName,
                                    FirstIp = "10.249.1.50",
                                    NextIp = "10.249.1.60",
                                    LastIp = "10.249.1.100"
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("NetworkProviders");
                issue.Message.Should().Be(
                    "The network '10.249.0.0/22' of provider 'default' overlaps with the network '10.249.1.0/24' of provider 'second-nat-provider'.");
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
