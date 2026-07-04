namespace Eryph.Core.VmAgent;

public class VmHostAgentOvnConfiguration
{
    /// <summary>
    /// The host's IP address used as the local endpoint of the OVN Geneve overlay tunnels
    /// (<c>ovn-encap-ip</c>). It must be an address reachable by the other chassis on the
    /// overlay transport network. Host-local and authoritative — the controller never
    /// distributes it. When unset the chassis falls back to the loopback address, which only
    /// works for a single-host (co-located) deployment; a multi-host deployment must set it.
    /// </summary>
    public string? OverlayTransportIp { get; init; }
}
