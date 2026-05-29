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
internal sealed class ConfigDistributionService(
    IStateStoreRepository<ConfigRecord> records,
    IEnumerable<IConfigSource> sources)
{
    // Which configuration domains each component type is entitled to receive.
    private static readonly IReadOnlyDictionary<ComponentType, ConfigDomain[]> Entitlements =
        new Dictionary<ComponentType, ConfigDomain[]>
        {
            // Host agents need the placement vocabulary (datastore/environment names).
            [ComponentType.VMHostAgent] = [ConfigDomain.PlacementConfig],
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
            var record = await GetOrCreateRecordAsync(domain, cancellationToken);
            if (record is null)
                continue;

            var known = knownVersions.TryGetValue(domain, out var v) ? v : 0;
            if (record.Version > known)
                bundles.Add(new ConfigBundle { Domain = domain, Version = record.Version, Payload = record.Payload });
        }

        return bundles;
    }

    /// <summary>
    /// Re-evaluates a domain against its source. Creates the record on first use,
    /// bumps the version when the payload changed, and returns the new bundle — or
    /// <c>null</c> when the content is unchanged (so callers can skip publishing).
    /// </summary>
    public async Task<ConfigBundle?> RefreshAsync(ConfigDomain domain, CancellationToken cancellationToken)
    {
        var source = sources.FirstOrDefault(s => s.Domain == domain);
        if (source is null)
            return null;

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
            return new ConfigBundle { Domain = domain, Version = record.Version, Payload = record.Payload };
        }

        if (record.Payload == payload)
            return null;

        record.Version++;
        record.Payload = payload;
        record.LastUpdated = DateTimeOffset.UtcNow;
        await records.UpdateAsync(record, cancellationToken);
        return new ConfigBundle { Domain = domain, Version = record.Version, Payload = record.Payload };
    }

    private async Task<ConfigRecord?> GetOrCreateRecordAsync(ConfigDomain domain, CancellationToken cancellationToken)
    {
        var record = await records.GetBySpecAsync(new ConfigRecordSpecs.GetByDomain(domain), cancellationToken);
        if (record is not null)
            return record;

        var source = sources.FirstOrDefault(s => s.Domain == domain);
        if (source is null)
            return null;

        var payload = await source.BuildPayloadAsync(cancellationToken);
        record = new ConfigRecord
        {
            Id = Guid.NewGuid(),
            Domain = domain,
            Version = 1,
            Payload = payload,
            LastUpdated = DateTimeOffset.UtcNow,
        };
        await records.AddAsync(record, cancellationToken);
        return record;
    }
}
