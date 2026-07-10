using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

internal sealed class ComponentConfigState : IComponentConfigState
{
    // Exactly ONE effective (scope, version) per domain: a component resolves and applies a single scope
    // per domain at a time, so applying a new scope replaces the previous one. Accumulating multiple
    // scopes would let a stale entry make a reverted scope look "already applied" and strand the
    // component on the wrong effective config.
    private readonly ConcurrentDictionary<ConfigDomain, (string Scope, long Version)> _applied = new();

    public void SetApplied(ConfigDomain domain, string scope, long version) =>
        _applied.AddOrUpdate(
            domain,
            (scope ?? "", version),
            (_, existing) => existing.Scope == (scope ?? "") && existing.Version >= version
                ? existing
                : (scope ?? "", version));

    public long GetAppliedVersion(ConfigDomain domain, string scope) =>
        _applied.TryGetValue(domain, out var applied) && applied.Scope == (scope ?? "")
            ? applied.Version
            : 0;

    // Ordered by domain so the reported list is deterministic: the list is sent in heartbeats and
    // persisted, so an unstable order would cause spurious registration updates.
    public IReadOnlyList<AppliedConfigVersion> GetApplied() =>
        _applied
            .OrderBy(kv => kv.Key)
            .Select(kv => new AppliedConfigVersion
            {
                Domain = kv.Key,
                Scope = kv.Value.Scope,
                Version = kv.Value.Version,
            })
            .ToList();
}
