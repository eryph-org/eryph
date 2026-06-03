using System;
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
    /// When <paramref name="publicKey"/> is supplied, the operator's key is written to the guest KVP
    /// slot <c>eryph:guest-services:client-public-key:{subjectId}</c> in OpenSSH authorized_keys form
    /// (with an <c>expiry-time</c> option when <paramref name="keyExpiry"/> is set) so the guest's
    /// <c>ClientKeyProvider</c> authorizes it. When <paramref name="publicKey"/> is null the flow is
    /// "pre-injected key" — no KVP write, the key was provisioned earlier.
    /// </para>
    /// <para>
    /// A one-time token is minted and held with a short grace window; the channel must be opened
    /// before it expires, and the token is single-use.
    /// </para>
    /// </summary>
    Task<ChannelRegistration> RegisterChannel(
        Guid vmId,
        string subjectId,
        string? publicKey,
        DateTimeOffset? keyExpiry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the operator's <paramref name="publicKey"/> to the guest KVP slot
    /// <c>eryph:guest-services:client-public-key:{subjectId}</c> in OpenSSH authorized_keys form (with an
    /// <c>expiry-time</c> option when <paramref name="keyExpiry"/> is set) so the guest's
    /// <c>ClientKeyProvider</c> authorizes it. This is the standalone "authorize my key" flow (BYOK); no
    /// channel is opened and no token is minted.
    /// </summary>
    Task AddKey(
        Guid vmId,
        string subjectId,
        string publicKey,
        DateTimeOffset? keyExpiry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and consumes <paramref name="token"/>, opens the guest hvsocket (the well-known EGS
    /// service, vsock port 5002), and returns it as a raw duplex byte stream the caller bridges to.
    /// Returns <see langword="null"/> when the token is unknown, already used, or expired. The caller
    /// owns and disposes the returned stream (disposing it closes the hvsocket).
    /// </summary>
    Task<Stream?> OpenChannelAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the operator's authorized-key KVP slot
    /// (<c>eryph:guest-services:client-public-key:{subjectId}</c>) on the guest running as
    /// <paramref name="vmId"/>. Used by the revoke flow.
    /// </summary>
    Task RemoveKey(Guid vmId, string subjectId, CancellationToken cancellationToken = default);
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
