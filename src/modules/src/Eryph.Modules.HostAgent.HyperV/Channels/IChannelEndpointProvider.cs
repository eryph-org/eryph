using Eryph.ModuleCore.Components;

namespace Eryph.Modules.HostAgent.Channels;

/// <summary>
/// Builds the wss URL of this agent's channel listener (with the one-time token embedded) that the
/// control-plane handler returns to the compute API as the channel endpoint.
/// </summary>
public interface IChannelEndpointProvider
{
    /// <summary>
    /// The base wss URL of this agent's channel listener (no token), e.g.
    /// <c>wss://{advertisedHost}:{port}</c>. This is the value advertised to the controller via the
    /// Endpoints config domain so the compute API can resolve the per-agent listener.
    /// </summary>
    string BaseUrl { get; }

    /// <summary>Builds the per-channel wss URL <c>{BaseUrl}/v1/channels/{token}</c>.</summary>
    string BuildChannelUrl(string token);
}

/// <summary>
/// Derives the listener URL from <see cref="ChannelListenerOptions"/>, falling back to the host FQDN
/// (<see cref="ComponentIdentity.GetLocalHostId"/>) when no advertised host is configured so the URL
/// matches a DNS SAN in the enrolled server certificate.
/// </summary>
public sealed class ChannelEndpointProvider(ChannelListenerOptions options)
    : IChannelEndpointProvider
{
    public string BaseUrl
    {
        get
        {
            var host = string.IsNullOrWhiteSpace(options.AdvertisedHost)
                ? ComponentIdentity.GetLocalHostId()
                : options.AdvertisedHost;
            return $"wss://{host}:{options.Port}";
        }
    }

    public string BuildChannelUrl(string token) => $"{BaseUrl}/v1/channels/{token}";
}
