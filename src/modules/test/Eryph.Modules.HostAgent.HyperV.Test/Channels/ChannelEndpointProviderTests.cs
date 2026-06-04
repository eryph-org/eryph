using Eryph.Modules.HostAgent.Channels;

namespace Eryph.Modules.HostAgent.HyperV.Test.Channels;

public class ChannelEndpointProviderTests
{
    [Fact]
    public void BaseUrl_UsesAdvertisedHostAndPort()
    {
        var provider = new ChannelEndpointProvider(new ChannelListenerOptions
        {
            AdvertisedHost = "agent.example",
            Port = 9700,
        });

        provider.BaseUrl.Should().Be("wss://agent.example:9700");
    }

    [Fact]
    public void BuildChannelUrl_EmbedsTokenInChannelPath()
    {
        var provider = new ChannelEndpointProvider(new ChannelListenerOptions
        {
            AdvertisedHost = "agent.example",
            Port = 9700,
        });

        provider.BuildChannelUrl("the-token")
            .Should().Be("wss://agent.example:9700/v1/channels/the-token");
    }
}
