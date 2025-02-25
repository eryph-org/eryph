using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Network;
using LanguageExt;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

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
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-nat",
                },
                new NetworkProvider
                {
                    Name = "default",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-nat-2",
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail();
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
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-nat",
                },
                new NetworkProvider
                {
                    Name = "second-provider",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-nat",
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail();
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
                },
                new NetworkProvider
                {
                    Name = "second-provider",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-pif-2",
                    Adapters = ["test-adapter-1"],
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail();
    }

    [Fact]
    public void ValidateNetworkProvidersConfig_AdapterIsNamedLikeBridge_ReturnsError()
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
                },
                new NetworkProvider
                {
                    Name = "test-overlay",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-int",
                    Adapters = ["br-nat", "br-int"]
                },
            ],
        };

        var result = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);

        result.Should().BeFail();
    }



    // TODO validate no gateway address and network
    // TODO validate no overlapping subnets
    // TODO validate no overlapping ip pools
    // TODO validate NAT overlay only has default subnet
    // TODO validate switch name only for flat provider
    // TODO validate no invalid names
}