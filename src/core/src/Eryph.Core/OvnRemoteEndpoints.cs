namespace Eryph.Core;

/// <summary>
/// The well-known names and ports under which the standalone network process exposes the OVN
/// databases to remote clients over SSL. Shared across the components so they agree on a single
/// source: the network process advertises these endpoints on registration and opens the listeners;
/// the controller resolves <see cref="NorthboundName"/> to reach the northbound database when the
/// network process runs on a different host (co-located it uses the local pipe instead).
/// </summary>
public static class OvnRemoteEndpoints
{
    public const int NorthboundPort = 6641;

    public const int SouthboundPort = 6642;

    public const string NorthboundName = "ovn-northbound";

    public const string SouthboundName = "ovn-southbound";
}
