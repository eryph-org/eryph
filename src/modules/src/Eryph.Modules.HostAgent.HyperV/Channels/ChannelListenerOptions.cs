namespace Eryph.Modules.HostAgent.Channels;

/// <summary>
/// Configuration for the agent's EGS channel listener (Kestrel). Bound from the
/// <c>egsChannel</c> configuration section.
/// </summary>
public sealed class ChannelListenerOptions
{
    public const string SectionName = "egsChannel";

    /// <summary>
    /// Whether the channel listener is started. Disabled by default; the split runtime enables it
    /// only when component mTLS is configured (the listener requires the enrolled server certificate
    /// and the component client CA trust bundle). In eryph-zero the listener is never started — the
    /// compute API reaches the agent through the in-process channel forwarder instead.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The IP address the listener binds to. Must be an address on the internal/management network
    /// reachable by the compute API, never a public interface. The compute API is the only permitted
    /// client and is enforced by mTLS (component client certificate), but binding to the internal
    /// address keeps the surface off any public NIC as defence in depth.
    /// </summary>
    public string BindAddress { get; set; } = "127.0.0.1";

    /// <summary>The TCP port the listener binds to.</summary>
    public int Port { get; set; } = 9700;

    /// <summary>
    /// The host name the compute API uses to reach this agent (must match a DNS SAN in the agent's
    /// enrolled server certificate so the client's TLS host-name check passes). Defaults to the host
    /// FQDN when unset.
    /// </summary>
    public string? AdvertisedHost { get; set; }

    /// <summary>
    /// The component certificate directory (where enrollment wrote <c>server.pfx</c> and
    /// <c>ca-bundle.pem</c>). Not part of the <see cref="SectionName"/> section — the module populates
    /// it from the shared <c>componentMtls:certificateDirectory</c> setting (the same directory the bus
    /// transport and ComponentServerTls use) so the listener and the bus stay on one source of truth.
    /// </summary>
    public string? CertificateDirectory { get; set; }
}
