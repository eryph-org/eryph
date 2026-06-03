using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.HostAgent.Channels;

/// <summary>
/// The host-agent side of the EGS remote channel. <see cref="RegisterChannel"/> (control plane) writes
/// the operator's authorized key to the guest KVP when supplied and mints a one-time token;
/// <see cref="OpenChannelAsync"/> (data plane) validates the token and opens the guest hvsocket,
/// returning it as a raw byte stream for the caller to bridge.
/// </summary>
public interface IChannelService
{
    /// <summary>
    /// Registers an intent to open an EGS channel to the guest running as <paramref name="vmId"/>.
    /// <para>
    /// <paramref name="accessKeyValues"/> are External-pool KVP values authorizing the operator's key
    /// (the slot/line built by the endpoint); they are written to the guest so its
    /// <c>ClientKeyProvider</c> authorizes the key. An empty map is the "pre-injected key" flow — no
    /// KVP write, the key was provisioned earlier.
    /// </para>
    /// <para>
    /// A one-time token is minted and held with a short grace window; the channel must be opened
    /// before it expires, and the token is single-use.
    /// </para>
    /// </summary>
    Task<ChannelRegistration> RegisterChannel(
        Guid vmId,
        IReadOnlyDictionary<string, string> accessKeyValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and consumes <paramref name="token"/>, opens the guest hvsocket (the well-known EGS
    /// service, vsock port 5002), and returns it as a raw duplex byte stream the caller bridges to.
    /// Returns <see langword="null"/> when the token is unknown, already used, or expired. The caller
    /// owns and disposes the returned stream (disposing it closes the hvsocket).
    /// </summary>
    Task<Stream?> OpenChannelAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of <see cref="IChannelService.RegisterChannel"/>, returned to the saga as the
/// <see cref="Messages.Resources.Catlets.Commands.OpenSshChannelVMCommandResponse"/>.
/// </summary>
public sealed class ChannelRegistration
{
    public required string Token { get; init; }

    /// <summary>The wss URL of this agent's network channel listener (<c>wss://{host}/v1/channels/{token}</c>).</summary>
    public required string AgentEndpoint { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
