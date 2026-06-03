using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Eryph.GuestServices.Core;
using Eryph.GuestServices.HvDataExchange.Host;
using Eryph.GuestServices.Sockets;
using Eryph.Modules.AspNetCore.Channels;

namespace Eryph.Modules.HostAgent.Channels;

/// <summary>
/// Default <see cref="IChannelService"/>: holds pending one-time channels keyed by token, writes/clears
/// the guest authorized-key KVP slot via <see cref="HostDataExchange"/>, and on open dials the guest
/// hvsocket with <see cref="SocketFactory.CreateClientSocket"/> and returns it as a stream.
/// </summary>
public class ChannelService(
    IHostDataExchange hostDataExchange,
    IChannelEndpointProvider endpointProvider,
    ILogger<ChannelService> logger)
    : IChannelService, IAgentChannelRecipient
{
    // One-time tokens with a grace window. The token only buys the window between the control plane
    // returning it and the channel being opened; after a successful open it is consumed and removed,
    // and a never-opened token is swept by its expiry. The window must cover the full control-plane
    // round-trip (operation dispatch over the bus → saga → agent → result → client poll → connect),
    // which on the split runtime crosses RabbitMQ, so it is generous rather than seconds-tight.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromSeconds(120);

    private readonly ConcurrentDictionary<string, PendingChannel> _pending = new(StringComparer.Ordinal);

    public async Task<ChannelRegistration> RegisterChannel(
        Guid vmId,
        string subjectId,
        string? publicKey,
        DateTimeOffset? keyExpiry,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("The subject id must be provided.", nameof(subjectId));

        // Added-key flow: publish the operator's key to the guest's authorized set. Pre-injected flow
        // (publicKey == null) skips the write.
        if (!string.IsNullOrWhiteSpace(publicKey))
            await AddKey(vmId, subjectId, publicKey, keyExpiry, cancellationToken).ConfigureAwait(false);

        var token = MintToken();
        var expiresAt = DateTimeOffset.UtcNow + TokenLifetime;
        var pending = new PendingChannel(vmId, expiresAt);

        if (!_pending.TryAdd(token, pending))
            // 256 bits of entropy: a collision is not realistically reachable, but fail closed rather
            // than silently overwrite an existing pending channel.
            throw new InvalidOperationException("A channel token collision occurred; retry the request.");

        SweepExpired();

        return new ChannelRegistration
        {
            Token = token,
            AgentEndpoint = endpointProvider.BuildChannelUrl(token),
            ExpiresAt = expiresAt,
        };
    }

    public async Task AddKey(
        Guid vmId,
        string subjectId,
        string publicKey,
        DateTimeOffset? keyExpiry,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("The subject id must be provided.", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(publicKey))
            throw new ArgumentException("The public key must be provided.", nameof(publicKey));

        // The guest's ClientKeyProvider reads exactly this slot family (Constants.ClientAuthKeyPrefix + id)
        // and honours the expiry-time option.
        var slotKey = Constants.ClientAuthKeyPrefix + subjectId;
        await hostDataExchange.SetExternalValuesAsync(
            vmId,
            new Dictionary<string, string?>
            {
                [slotKey] = BuildAuthorizedKeyLine(publicKey, keyExpiry),
            }).ConfigureAwait(false);
    }

    public async Task<Stream?> OpenChannelAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!TryConsumeToken(token, out var vmId))
            return null;

        var stream = await ConnectGuestAsync(vmId, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Opened EGS channel to guest {VmId}.", vmId);
        return stream;
    }

    // Dials the guest's EGS service (always the well-known service id; vsock port 5002). The returned
    // Socket is already connected; the NetworkStream owns it, so disposing the stream (the caller's
    // responsibility) closes the hvsocket. Virtual so the token-consumption path can be exercised
    // without a live guest hvsocket.
    protected virtual async Task<Stream> ConnectGuestAsync(Guid vmId, CancellationToken cancellationToken)
    {
        var socket = await SocketFactory.CreateClientSocket(vmId, Constants.ServiceId).ConfigureAwait(false);
        return new NetworkStream(socket, ownsSocket: true);
    }

    public async Task RemoveKey(Guid vmId, string subjectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("The subject id must be provided.", nameof(subjectId));

        // SetExternalValuesAsync removes a key when its value is null (HostDataExchange maps null to
        // a RemoveKvpItems call). The slot key must match exactly what RegisterChannel wrote.
        var slotKey = Constants.ClientAuthKeyPrefix + subjectId;
        await hostDataExchange.SetExternalValuesAsync(
            vmId,
            new Dictionary<string, string?>
            {
                [slotKey] = null,
            }).ConfigureAwait(false);
    }

    // OpenSSH authorized_keys line: an optional leading `expiry-time="..."` option then the key body.
    // The timestamp must be the OpenSSH compact UTC form "yyyyMMddHHmmssZ" (no 'T' separator) — the
    // guest's ClientKeyProvider rejects other forms and treats an unparseable expiry as expired.
    private static string BuildAuthorizedKeyLine(string publicKey, DateTimeOffset? keyExpiry)
    {
        var key = publicKey.Trim();
        if (keyExpiry is not { } expiry)
            return key;

        var expiryText = expiry.ToUniversalTime().ToString("yyyyMMddHHmmss'Z'", CultureInfo.InvariantCulture);
        return $"expiry-time=\"{expiryText}\" {key}";
    }

    private bool TryConsumeToken(string token, out Guid vmId)
    {
        vmId = default;
        if (string.IsNullOrEmpty(token) || !_pending.TryRemove(token, out var pending))
            return false;

        if (pending.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        vmId = pending.VmId;
        return true;
    }

    private static string MintToken()
    {
        // 256 bits, URL-safe (the token rides in the URL path /v1/channels/{token}).
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private void SweepExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (token, pending) in _pending)
        {
            if (pending.ExpiresAt <= now)
                _pending.TryRemove(token, out _);
        }
    }

    private sealed record PendingChannel(Guid VmId, DateTimeOffset ExpiresAt);
}
