using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

internal sealed class ComponentConfigState : IComponentConfigState
{
    private readonly ConcurrentDictionary<(ConfigDomain Domain, string Scope), long> _applied = new();

    public void SetApplied(ConfigDomain domain, string scope, long version) =>
        _applied.AddOrUpdate(
            (domain, scope), version, (_, existing) => version > existing ? version : existing);

    public long GetAppliedVersion(ConfigDomain domain, string scope) =>
        _applied.GetValueOrDefault((domain, scope), 0);

    // Ordered by (domain, scope) so the reported list is deterministic: ConcurrentDictionary enumeration
    // order is not stable, and this list is sent in heartbeats and persisted, so an unstable order would
    // cause spurious registration updates on every beat even when the content is unchanged.
    public IReadOnlyList<AppliedConfigVersion> GetApplied() =>
        _applied
            .OrderBy(kv => kv.Key.Domain)
            .ThenBy(kv => kv.Key.Scope, StringComparer.Ordinal)
            .Select(kv => new AppliedConfigVersion
            {
                Domain = kv.Key.Domain,
                Scope = kv.Key.Scope,
                Version = kv.Value,
            })
            .ToList();
}
