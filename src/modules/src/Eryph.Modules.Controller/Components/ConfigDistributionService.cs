using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.DistributedLock;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Resolves the configuration a component receives and materializes the per-(domain, scope)
/// <see cref="ConfigRecord"/> for it. A component gets the most-specific authored value among the
/// scopes it selects (its environment, tags and host id); system-derived domains are global (default
/// scope). Resolution is entirely controller-side — a component still receives one payload per domain.
/// </summary>
/// <remarks>
/// The read-modify-write in <c>EnsureCurrentRecordAsync</c> is serialized per (domain, scope) by a
/// distributed lock, so concurrent workers/controllers cannot both insert (unique-index collision) or
/// lose a version bump. The lock is held for the remainder of the message unit of work.
/// </remarks>
internal sealed class ConfigDistributionService(
    IStateStoreRepository<ConfigRecord> records,
    IEnumerable<IConfigSource> sources,
    IAuthoredConfigStore authoredStore,
    IDistributedLockScopeHolder lockHolder)
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(1);

    public ConfigDomain[] GetEntitledDomains(ComponentType componentType) =>
        ComponentConfigEntitlements.GetEntitledDomains(componentType);

    /// <summary>
    /// The scope a component resolves for a domain: the most-specific scope it selects that has an
    /// authored value, else the default scope (system-derived / settings fallback). Only authorable
    /// domains can have a non-default scope.
    /// </summary>
    private async Task<string> ResolveScopeAsync(
        ConfigDomain domain, ComponentRegistration registration, CancellationToken cancellationToken)
    {
        // System-derived and non-scopable domains (e.g. the single global network topology) only ever
        // have a default-scope value, so never resolve a more-specific scope for them.
        if (!ConfigDomainDescriptors.IsAuthorable(domain) || !ConfigDomainDescriptors.SupportsScopedAuthoring(domain))
            return ConfigScope.Default;

        // Walk the component's scopes most-specific first and stop at the first that has an authored
        // value. The default scope is the terminal fallback and is not probed here: whether it has an
        // authored value is irrelevant (the source builds the default from the authored value or the
        // settings file), so returning it unconditionally is correct.
        foreach (var scope in ConfigScope.ResolutionOrder(registration))
        {
            if (scope == ConfigScope.Default)
                break;
            if (await authoredStore.GetCurrentAsync(domain, scope, cancellationToken) is not null)
                return scope;
        }

        return ConfigScope.Default;
    }

    /// <summary>
    /// Builds the snapshot bundles a component is entitled to and does not already hold, resolving each
    /// domain at the component's scope and materializing the record on first use.
    /// </summary>
    public async Task<List<ConfigBundle>> BuildSnapshotAsync(
        ComponentRegistration registration,
        IReadOnlyList<AppliedConfigVersion> knownVersions,
        CancellationToken cancellationToken)
    {
        // The component reports its known versions per (domain, scope); index them for lookup. The
        // reported set is authoritative for the pull path (it may be a fresh/reset component), so it is
        // used instead of the stored registration state.
        var known = new Dictionary<(ConfigDomain, string), long>();
        foreach (var version in knownVersions)
        {
            var key = (version.Domain, version.Scope);
            known[key] = Math.Max(known.GetValueOrDefault(key), version.Version);
        }

        var bundles = new List<ConfigBundle>();
        foreach (var domain in GetEntitledDomains(registration.ComponentType))
        {
            var bundle = await ResolveBundleAsync(
                domain, registration,
                (d, s) => known.GetValueOrDefault((d, s)), materialize: true, cancellationToken);
            if (bundle is not null)
                bundles.Add(bundle);
        }

        return bundles;
    }

    /// <summary>
    /// Heartbeat drift: compares a component against the already-materialized records at its resolved
    /// scopes. Does not re-evaluate the sources (no materialization). A component moved to a new scope
    /// has applied version 0 there, so it is naturally re-pushed the scope's current record.
    /// </summary>
    public async Task<List<ConfigBundle>> GetOutdatedBundlesAsync(
        ComponentRegistration registration,
        CancellationToken cancellationToken)
    {
        var bundles = new List<ConfigBundle>();
        foreach (var domain in GetEntitledDomains(registration.ComponentType))
        {
            var bundle = await ResolveBundleAsync(
                domain, registration, registration.GetAppliedVersion, materialize: false, cancellationToken);
            if (bundle is not null)
                bundles.Add(bundle);
        }

        return bundles;
    }

    /// <summary>
    /// Re-evaluates a domain at the component's resolved scope and returns the bundle when the component
    /// does not already hold it; <c>null</c> otherwise (already current, or no source).
    /// </summary>
    public async Task<ConfigBundle?> RefreshForComponentAsync(
        ConfigDomain domain, ComponentRegistration registration, CancellationToken cancellationToken) =>
        await ResolveBundleAsync(
            domain, registration, registration.GetAppliedVersion, materialize: true, cancellationToken);

    /// <summary>
    /// Decides whether a component needs the current value of a domain and, if so, returns the bundle.
    /// The component's applied version is looked up for the RESOLVED scope; because each (domain, scope)
    /// has an independent counter, a component moved to a new scope has applied 0 there and is correctly
    /// pushed that scope's record even when its version is lower than what the component applied under
    /// its previous scope. The bundle carries the scope so the component tracks and acknowledges per
    /// scope in turn.
    /// </summary>
    private async Task<ConfigBundle?> ResolveBundleAsync(
        ConfigDomain domain,
        ComponentRegistration registration,
        Func<ConfigDomain, string, long> getAppliedVersion,
        bool materialize,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveScopeAsync(domain, registration, cancellationToken);

        var record = materialize
            ? (await EnsureCurrentRecordAsync(domain, scope, cancellationToken)).Record
            : await records.GetBySpecAsync(
                new ConfigRecordSpecs.GetByDomainAndScope(domain, scope), cancellationToken);
        if (record is null)
            return null;

        if (record.Version <= getAppliedVersion(domain, scope))
            return null;

        return new ConfigBundle
        {
            Domain = domain,
            Scope = scope,
            Version = record.Version,
            Payload = record.Payload,
        };
    }

    /// <summary>
    /// Materializes the record for (domain, scope) from its source: creates it on first use, bumps the
    /// version when the payload changed, otherwise leaves it untouched.
    /// </summary>
    private async Task<(ConfigRecord? Record, bool Changed)> EnsureCurrentRecordAsync(
        ConfigDomain domain, string scope, CancellationToken cancellationToken)
    {
        var source = sources.FirstOrDefault(s => s.Domain == domain);
        if (source is null)
            return (null, false);

        // Serialize the whole build-read-modify-write for this (domain, scope). The scope is
        // percent-escaped so a selector cannot introduce an invalid lock-file character.
        await lockHolder.AcquireLock(
            $"config-domain-{domain}-{Uri.EscapeDataString(scope)}", LockTimeout);

        var payload = await source.BuildPayloadAsync(scope, cancellationToken);
        var record = await records.GetBySpecAsync(
            new ConfigRecordSpecs.GetByDomainAndScope(domain, scope), cancellationToken);

        if (record is null)
        {
            record = new ConfigRecord
            {
                Id = Guid.NewGuid(),
                Domain = domain,
                Scope = scope,
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
