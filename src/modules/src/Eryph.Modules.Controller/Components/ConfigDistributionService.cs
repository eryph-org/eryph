using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// The read-then-insert in <c>EnsureCurrentRecordAsync</c> is not
/// guarded by an optimistic-concurrency token. This is safe today because the
/// controller dispatches bus messages serially within a single process. When the
/// in-memory bus is replaced by a real broker (a later cluster phase), two
/// concurrent first-touches of the same domain could both insert and collide on the
/// unique <c>ConfigRecord.Domain</c> index — that phase must add retry-on-conflict
/// or a concurrency token here.
/// </remarks>
internal sealed class ConfigDistributionService(
    IStateStoreRepository<ConfigRecord> records,
    IEnumerable<IConfigSource> sources)
{
    // Which configuration domains each component type is entitled to receive.
    private static readonly IReadOnlyDictionary<ComponentType, ConfigDomain[]> Entitlements =
        new Dictionary<ComponentType, ConfigDomain[]>
        {
            // Host agents need the placement vocabulary (datastore/environment names)
            // and the network-provider configuration to realize host networking.
            [ComponentType.VMHostAgent] = [ConfigDomain.PlacementConfig, ConfigDomain.NetworkProviders],
        };

    public ConfigDomain[] GetEntitledDomains(ComponentType componentType) =>
        Entitlements.TryGetValue(componentType, out var domains) ? domains : [];

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

            var known = knownVersions.TryGetValue(domain, out var v) ? v : 0;
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
