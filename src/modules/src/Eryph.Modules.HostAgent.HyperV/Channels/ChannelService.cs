using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Eryph.GuestServices.Core;
using Eryph.GuestServices.Sockets;
using Eryph.Modules.AspNetCore.Channels;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent.Channels;

/// <summary>
/// Default <see cref="IChannelService"/>: holds pending one-time channels keyed by token, writes the
/// operator's authorized-key KVP values via <see cref="IGuestDataWriter"/>, and on open dials the guest
/// hvsocket with <see cref="SocketFactory.CreateClientSocket"/> and returns it as a stream.
/// </summary>
public class ChannelService : IChannelService, IAgentChannelRecipient, IDisposable
{
    // One-time tokens with a grace window. The token only buys the window between the control plane
    // returning it and the channel being opened; after a successful open it is consumed and removed,
    // and a never-opened token is swept by its expiry. The window must cover the full control-plane
    // round-trip (operation dispatch over the bus → saga → agent → result → client poll → connect),
    // which on the split runtime crosses RabbitMQ, so it is generous rather than seconds-tight.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromSeconds(120);
    private readonly IChannelEndpointProvider _endpointProvider;

    private readonly IGuestDataWriter _guestDataWriter;
    private readonly ILogger<ChannelService> _logger;
    private readonly ConcurrentDictionary<string, PendingChannel> _pending = new(StringComparer.Ordinal);
    private readonly Timer _sweepTimer;

    public ChannelService(
        IGuestDataWriter guestDataWriter,
        IChannelEndpointProvider endpointProvider,
        ILogger<ChannelService> logger)
    {
        _guestDataWriter = guestDataWriter;
        _endpointProvider = endpointProvider;
        _logger = logger;
        // Sweep expired pending tokens on a timer (not only when a new channel is registered) so a burst
        // of never-opened tokens cannot accumulate in this long-lived process once registrations stop.
        _sweepTimer = new Timer(_ => SweepExpired(), null, TokenLifetime, TokenLifetime);
    }

    public async Task<ChannelRegistration> RegisterChannel(
        Guid vmId,
        IReadOnlyDictionary<string, string> accessKeyValues,
        CancellationToken cancellationToken = default)
    {
        // Added-key flow: publish the operator's authorized key to the guest. Pre-injected flow
        // (empty map) skips the write.
        if (accessKeyValues is { Count: > 0 })
            await _guestDataWriter.SetExternalAsync(
                vmId,
                accessKeyValues.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value)).ConfigureAwait(false);

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
            AgentEndpoint = _endpointProvider.BuildChannelUrl(token),
            ExpiresAt = expiresAt,
        };
    }

    public async Task<Stream?> OpenChannelAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!TryConsumeToken(token, out var vmId))
            return null;

        var stream = await ConnectGuestAsync(vmId, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Opened EGS channel to guest {VmId}.", vmId);
        return stream;
    }

    public void Dispose() => _sweepTimer.Dispose();

    // Dials the guest's EGS service (always the well-known service id; vsock port 5002). The returned
    // Socket is already connected; the NetworkStream owns it, so disposing the stream (the caller's
    // responsibility) closes the hvsocket. Virtual so the token-consumption path can be exercised
    // without a live guest hvsocket.
    protected virtual async Task<Stream> ConnectGuestAsync(Guid vmId, CancellationToken cancellationToken)
    {
        var socket = await SocketFactory.CreateClientSocket(vmId, Constants.ServiceId).ConfigureAwait(false);
        return new NetworkStream(socket, true);
    }

    private bool TryConsumeToken(string token, out Guid vmId)
    {
        vmId = Guid.Empty;
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
            if (pending.ExpiresAt <= now)
                _pending.TryRemove(token, out _);
    }

    private sealed record PendingChannel(Guid VmId, DateTimeOffset ExpiresAt);
}
