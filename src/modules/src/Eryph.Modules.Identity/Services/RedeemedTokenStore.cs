using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Records which one-time enrollment tokens have been redeemed, so a token cannot be redeemed twice.
/// Must be a <b>singleton</b> — it is the state that enforces single use. (The token service itself is
/// transient, matching the transient cross-wired certificate services, so the redeemed set lives
/// here rather than on the service instance.)
/// </summary>
public interface IRedeemedTokenStore
{
    /// <summary>Atomically claims a token id; returns false if it was already redeemed.</summary>
    bool TryRedeem(string jti, DateTimeOffset expiresAt);
}

/// <inheritdoc />
/// <remarks>
/// In-memory: an identity restart within a token's (short) lifetime would allow that token to be
/// redeemed again. Persisting redeemed ids across restarts is a follow-up; the short token lifetime
/// is the interim mitigation.
/// </remarks>
public sealed class RedeemedTokenStore : IRedeemedTokenStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _redeemed = new();

    public bool TryRedeem(string jti, DateTimeOffset expiresAt)
    {
        PruneExpired();
        return _redeemed.TryAdd(jti, expiresAt);
    }

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _redeemed.Where(e => e.Value <= now).ToList())
            _redeemed.TryRemove(entry.Key, out _);
    }
}
