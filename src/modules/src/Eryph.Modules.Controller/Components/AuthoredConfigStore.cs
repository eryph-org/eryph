using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.DistributedLock;
using Eryph.Messages.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Reads and appends immutable, operator-authored versions of a configuration domain (see
/// <see cref="AuthoredConfig"/>). The current value is the highest version; a new version is only
/// ever appended, so history is preserved and a rollback is a new version carrying an earlier payload.
/// </summary>
internal interface IAuthoredConfigStore
{
    /// <summary>The current (highest-version) authored value for a domain/scope, or null if none.</summary>
    Task<AuthoredConfig?> GetCurrentAsync(ConfigDomain domain, string scope, CancellationToken cancellationToken);

    /// <summary>Appends a new version for a domain/scope and returns it.</summary>
    Task<AuthoredConfig> AddVersionAsync(
        ConfigDomain domain, string scope, string payload, string? author, CancellationToken cancellationToken);

    /// <summary>The full version history for a domain/scope, newest first.</summary>
    Task<IReadOnlyList<AuthoredConfig>> GetHistoryAsync(
        ConfigDomain domain, string scope, CancellationToken cancellationToken);
}

internal sealed class AuthoredConfigStore(
    IStateStoreRepository<AuthoredConfig> repository,
    IDistributedLockScopeHolder lockHolder)
    : IAuthoredConfigStore
{
    // Appending a version touches only the state DB and should be near-instant; a long wait means
    // contention or a stuck unit of work, so fail (and let the bus retry) rather than block a worker.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(1);

    public Task<AuthoredConfig?> GetCurrentAsync(
        ConfigDomain domain, string scope, CancellationToken cancellationToken) =>
        repository.GetBySpecAsync(new AuthoredConfigSpecs.GetCurrent(domain, scope), cancellationToken);

    public async Task<AuthoredConfig> AddVersionAsync(
        ConfigDomain domain, string scope, string payload, string? author, CancellationToken cancellationToken)
    {
        // Serialize appends per domain/scope on this controller so two concurrent authors do not read
        // the same current version and both allocate the same next one. The lock is host-local (the
        // provider is file-based), so across multiple controllers the real guard is the unique
        // (Domain, Scope, Version) index: a colliding insert is rejected and the message unit of work
        // retries, re-reading the committed version and allocating the next one — the write converges,
        // it is not lost. Held until the message unit of work completes.
        await lockHolder.AcquireLock($"authored-config:{domain}:{scope}", LockTimeout);

        var current = await repository.GetBySpecAsync(
            new AuthoredConfigSpecs.GetCurrent(domain, scope), cancellationToken);

        var entry = new AuthoredConfig
        {
            Id = Guid.NewGuid(),
            Domain = domain,
            Scope = scope,
            Version = (current?.Version ?? 0) + 1,
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = author,
        };
        await repository.AddAsync(entry, cancellationToken);
        return entry;
    }

    public async Task<IReadOnlyList<AuthoredConfig>> GetHistoryAsync(
        ConfigDomain domain, string scope, CancellationToken cancellationToken) =>
        await repository.ListAsync(new AuthoredConfigSpecs.GetHistory(domain, scope), cancellationToken);
}
