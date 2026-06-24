using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.DistributedLock;
using Eryph.Messages.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Decides which configuration domains a component type is entitled to and builds
/// the versioned bundles to send it, materializing a <see cref="ConfigRecord"/>
/// per domain on first use.
/// </summary>
/// <remarks>
/// The read-modify-write in <c>EnsureCurrentRecordAsync</c> is serialized per domain by a
/// distributed lock. The controller processes bus messages on multiple Rebus workers (and a
/// cluster may run multiple controllers), so without it two concurrent first-touches of the
/// same domain could both observe no record and insert, colliding on the unique
/// <c>ConfigRecord.Domain</c> index, or lose a concurrent version bump. The lock is held for
/// the remainder of the message unit of work.
/// </remarks>
internal sealed class ConfigDistributionService(
    IStateStoreRepository<ConfigRecord> records,
    IEnumerable<IConfigSource> sources,
    IDistributedLockScopeHolder lockHolder)
{
    // A first-touch/version-bump touches only the state DB and should be near-instant; a long
    // wait means contention or a stuck unit of work, so fail (and let the bus retry) rather
    // than block a worker indefinitely.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(1);

    // Which configuration domains each component type is entitled to receive.
    private static readonly IReadOnlyDictionary<ComponentType, ConfigDomain[]> Entitlements =
        new Dictionary<ComponentType, ConfigDomain[]>
        {
            // Host agents need the placement vocabulary (datastore/environment names)
            // and the network-provider configuration to realize host networking, plus
            // the deployment endpoints (e.g. the identity issuer) to reach other components.
            [ComponentType.VMHostAgent] =
                [ConfigDomain.PlacementConfig, ConfigDomain.NetworkProviders, ConfigDomain.Endpoints],
        };

    public ConfigDomain[] GetEntitledDomains(ComponentType componentType) =>
        // Return a fresh array so a caller cannot mutate the shared entitlement definition.
        Entitlements.TryGetValue(componentType, out var domains) ? [.. domains] : [];

    /// <summary>
    /// Builds the snapshot bundles a component is entitled to and does not already
    /// hold at the current version.
    /// </summary>
    public async Task<List<ConfigBundle>> BuildSnapshotAsync(
        ComponentType componentType,
        IReadOnlyDictionary<ConfigDomain, long> knownVersions,
        CancellationToken cancellationToken)
    {
        var bundles = new List<ConfigBundle>();
        foreach (var domain in GetEntitledDomains(componentType))
        {
            // Re-evaluate the source on every pull so a request always reflects the
            // current controller settings — not a record frozen at first use.
            var (record, _) = await EnsureCurrentRecordAsync(domain, cancellationToken);
            if (record is null)
                continue;

            var known = knownVersions.GetValueOrDefault(domain, 0);
            if (record.Version > known)
                bundles.Add(new ConfigBundle { Domain = domain, Version = record.Version, Payload = record.Payload });
        }

        return bundles;
    }

    /// <summary>
    /// Re-evaluates a domain against its source and returns the new bundle when the
    /// payload changed — or <c>null</c> when nothing changed (so the push path can
    /// skip publishing). The pull path uses <see cref="EnsureCurrentRecordAsync"/>
    /// directly because it must send the current record even when this call did not
    /// change it.
    /// </summary>
    public async Task<ConfigBundle?> RefreshAsync(ConfigDomain domain, CancellationToken cancellationToken)
    {
        var (record, changed) = await EnsureCurrentRecordAsync(domain, cancellationToken);
        if (record is null || !changed)
            return null;

        return new ConfigBundle { Domain = domain, Version = record.Version, Payload = record.Payload };
    }

    /// <summary>
    /// Materializes the record from its source: creates it on first use, bumps the
    /// version when the payload changed, and otherwise leaves it untouched. Returns
    /// the current record (reflecting the latest source) and whether this call
    /// changed it, or <c>(null, false)</c> when no source owns the domain.
    /// </summary>
    private async Task<(ConfigRecord? Record, bool Changed)> EnsureCurrentRecordAsync(
        ConfigDomain domain, CancellationToken cancellationToken)
    {
        var source = sources.FirstOrDefault(s => s.Domain == domain);
        if (source is null)
            return (null, false);

        // Serialize the whole build-read-modify-write for this domain so concurrent
        // workers/controllers cannot both insert (unique-index collision), lose a version
        // bump, or overwrite a newer payload with an older one. The payload is built under the
        // lock too: a slower builder acquiring the lock later would otherwise revert the record
        // to its stale payload. Held until the message unit of work completes.
        await lockHolder.AcquireLock($"config-domain-{domain}", LockTimeout);

        var payload = await source.BuildPayloadAsync(cancellationToken);
        var record = await records.GetBySpecAsync(new ConfigRecordSpecs.GetByDomain(domain), cancellationToken);

        if (record is null)
        {
            record = new ConfigRecord
            {
                Id = Guid.NewGuid(),
                Domain = domain,
                Version = 1,
                Payload = payload,
                LastUpdated = DateTimeOffset.UtcNow,
            };
            await records.AddAsync(record, cancellationToken);
            return (record, true);
        }

        if (record.Payload == payload)
            return (record, false);

        record.Version++;
        record.Payload = payload;
        record.LastUpdated = DateTimeOffset.UtcNow;
        await records.UpdateAsync(record, cancellationToken);
        return (record, true);
    }
}
