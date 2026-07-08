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
    IStateStoreRepository<ComponentRegistration> registrations,
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
        IReadOnlyDictionary<ConfigDomain, long> knownVersions,
        CancellationToken cancellationToken)
    {
        var bundles = new List<ConfigBundle>();
        foreach (var domain in GetEntitledDomains(registration.ComponentType))
        {
            var bundle = await ResolveBundleAsync(
                domain, registration, knownVersions, materialize: true, cancellationToken);
            if (bundle is not null)
                bundles.Add(bundle);
        }

        if (bundles.Count > 0)
            await PersistDistributedScopesAsync(registration, cancellationToken);
        return bundles;
    }

    /// <summary>
    /// Heartbeat drift: compares a component against the already-materialized records at its resolved
    /// scopes. Does not re-evaluate the sources (no materialization), but does force a re-push when the
    /// resolved scope changed since the component was last distributed.
    /// </summary>
    public async Task<List<ConfigBundle>> GetOutdatedBundlesAsync(
        ComponentRegistration registration,
        IReadOnlyDictionary<ConfigDomain, long> appliedVersions,
        CancellationToken cancellationToken)
    {
        var bundles = new List<ConfigBundle>();
        foreach (var domain in GetEntitledDomains(registration.ComponentType))
        {
            var bundle = await ResolveBundleAsync(
                domain, registration, appliedVersions, materialize: false, cancellationToken);
            if (bundle is not null)
                bundles.Add(bundle);
        }

        if (bundles.Count > 0)
            await PersistDistributedScopesAsync(registration, cancellationToken);
        return bundles;
    }

    /// <summary>
    /// Re-evaluates a domain at the component's resolved scope and returns the bundle when the component
    /// does not already hold it; <c>null</c> otherwise (already current, or no source).
    /// </summary>
    public async Task<ConfigBundle?> RefreshForComponentAsync(
        ConfigDomain domain, ComponentRegistration registration, CancellationToken cancellationToken)
    {
        var bundle = await ResolveBundleAsync(
            domain, registration, registration.AppliedConfigVersions, materialize: true, cancellationToken);
        if (bundle is not null)
            await PersistDistributedScopesAsync(registration, cancellationToken);
        return bundle;
    }

    /// <summary>
    /// Decides whether a component needs the current value of a domain and, if so, returns the bundle and
    /// records the resolved scope on the registration (in memory). A push is due when the component's
    /// resolved scope changed since it was last distributed — a scope change is invisible to a plain
    /// version comparison because each (domain, scope) has an independent counter — or when it is behind
    /// the current version within the same scope.
    /// </summary>
    private async Task<ConfigBundle?> ResolveBundleAsync(
        ConfigDomain domain,
        ComponentRegistration registration,
        IReadOnlyDictionary<ConfigDomain, long> appliedVersions,
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

        // A recorded distributed scope that differs from the resolved one means the component was moved
        // onto a different authored value; its applied version belongs to the old scope's counter and is
        // not comparable, so force the push. An absent entry (never distributed / pre-upgrade row) falls
        // back to the version comparison, which is correct when the scope has not changed.
        var lastScope = registration.DistributedConfigScopes.GetValueOrDefault(domain);
        var scopeChanged = lastScope is not null && lastScope != scope;

        var applied = appliedVersions.GetValueOrDefault(domain, 0);
        if (!scopeChanged && record.Version <= applied)
            return null;

        registration.DistributedConfigScopes[domain] = scope;
        return new ConfigBundle { Domain = domain, Version = record.Version, Payload = record.Payload };
    }

    // Saves the registration when its distributed-scope map was touched. Skips the synthetic fallback
    // registration used for an as-yet-unregistered requester (no persistent identity) so it is not
    // inserted; that component records its scopes once it is properly registered.
    private async Task PersistDistributedScopesAsync(
        ComponentRegistration registration, CancellationToken cancellationToken)
    {
        if (registration.Id != Guid.Empty)
            await registrations.UpdateAsync(registration, cancellationToken);
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
