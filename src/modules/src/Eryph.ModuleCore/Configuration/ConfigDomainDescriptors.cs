using System;
using System.Collections.Generic;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Configuration;

/// <summary>
/// Describes which configuration domains an operator may author via the management API and how to
/// validate a proposed payload. Domains not listed here are system-derived (built from controller
/// state by their <c>IConfigSource</c>) and must not be authored — accepting one would persist a value
/// no source ever reads and misrepresent the effective configuration.
/// </summary>
/// <remarks>
/// Authored config is <b>YAML</b>, matching eryph's config convention everywhere else (controller
/// settings, network providers, catlets). Validation round-trips the payload through the domain's YAML
/// serializer, which rejects malformed or unknown-member YAML and returns the <b>canonical</b> form
/// (re-serialized), so semantically-identical payloads that differ only in whitespace or key order do
/// not create noisy versions or redundant redistributions. Each domain's serializer produces exactly
/// the wire format that domain's <c>IConfigRealizer</c> consumes.
/// </remarks>
public static class ConfigDomainDescriptors
{
    private delegate string CanonicalizeDelegate(string payload);

    private static readonly IReadOnlyDictionary<ConfigDomain, CanonicalizeDelegate> Authorable =
        new Dictionary<ConfigDomain, CanonicalizeDelegate>
        {
            [ConfigDomain.StorageConfig] = CanonicalizeStorageConfig,

            [ConfigDomain.NetworkProviders] = CanonicalizeNetworkProviders,

            [ConfigDomain.Environments] = CanonicalizeEnvironments,
        };

    // As with the storage config, names are lower-cased to their canonical form (environment and
    // site resolution is case-insensitive) so an authored 'Prod' cannot diverge from a catlet's
    // 'prod'. An omitted site is filled with the default site here, at the authoring boundary, so
    // the stored payload always names one and no consumer has to guess.
    private static string CanonicalizeEnvironments(string payload)
    {
        var config = EnvironmentsConfigYamlSerializer.Deserialize(payload);
        NormalizeEnvironmentNames(config);

        var errors = EnvironmentsConfigValidation.Validate(config);
        if (errors.Count > 0)
            throw InvalidConfigExceptionFactory.Create(new Exception(
                "The environment configuration is invalid: " + string.Join("; ", errors)));

        return EnvironmentsConfigYamlSerializer.Serialize(config);
    }

    private static void NormalizeEnvironmentNames(EnvironmentsConfig config)
    {
        config.Environments ??= [];

        foreach (var environment in config.Environments)
        {
            if (environment is null)
                continue;

            // A null list item or `name: ~` leaves Name null; leave it for Validate to reject with a
            // proper "must not be empty" message rather than NRE-ing here on Trim().
            if (environment.Name is not null)
                environment.Name = environment.Name.Trim().ToLowerInvariant();

            environment.Site = string.IsNullOrWhiteSpace(environment.Site)
                ? EryphConstants.DefaultSiteName
                : environment.Site.Trim().ToLowerInvariant();
        }
    }

    // The serializer only checks shape; the agent enforces the real rules on the merged result (name
    // grammar, fully-qualified paths, no duplicates). Run those here so an invalid payload is rejected
    // when authored instead of distributed to fail on every agent in a retry loop. Names are lower-cased
    // to their canonical form (datastore/environment resolution is case-insensitive) so an authored
    // 'Fast' cannot diverge from a catlet's 'fast'.
    private static string CanonicalizeStorageConfig(string payload)
    {
        var config = StorageConfigYamlSerializer.Deserialize(payload);
        NormalizeStorageNames(config);

        var errors = StorageConfigValidation.Validate(config);
        if (errors.Count > 0)
            throw InvalidConfigExceptionFactory.Create(new Exception(
                "The storage configuration is invalid: " + string.Join("; ", errors)));

        return StorageConfigYamlSerializer.Serialize(config);
    }

    private static void NormalizeStorageNames(StorageConfig config)
    {
        config.Datastores ??= [];
        config.Environments ??= [];

        // A null list item or `name: ~` leaves Name null; leave it for Validate to reject with a proper
        // "must not be empty" message rather than NRE-ing here on Trim().
        foreach (var datastore in config.Datastores)
            if (datastore?.Name is not null)
                datastore.Name = datastore.Name.Trim().ToLowerInvariant();

        foreach (var environment in config.Environments)
        {
            if (environment is null)
                continue;
            if (environment.Name is not null)
                environment.Name = environment.Name.Trim().ToLowerInvariant();
            environment.Datastores ??= [];
            foreach (var datastore in environment.Datastores)
                if (datastore?.Name is not null)
                    datastore.Name = datastore.Name.Trim().ToLowerInvariant();
        }
    }

    // Network provider config has richer semantic rules than the shape check the serializer does
    // (overlapping NAT subnets, per-type field restrictions, IP-pool bounds), so validate explicitly.
    // The IP-pool next-IP cursor is runtime allocation state, not authored config, so strip it — the
    // controller keeps the cursor in its own state, and authored versions must not churn on allocation.
    private static string CanonicalizeNetworkProviders(string payload)
    {
        // A null document ("~"/"null") deserializes to an empty config (the serializer coalesces), which
        // then fails validation with a proper error instead of throwing out of the handler.
        var config = NetworkProvidersConfigYamlSerializer.Deserialize(payload);

        var validation = NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(config);
        if (validation.IsFail)
            throw InvalidConfigExceptionFactory.Create(new Exception(
                "The network provider configuration is invalid: "
                + string.Join("; ", validation.FailToSeq().Map(issue => issue.Message))));

        foreach (var provider in config.NetworkProviders ?? [])
        foreach (var subnet in provider.Subnets ?? [])
        foreach (var pool in subnet.IpPools ?? [])
            pool.NextIp = null;

        return NetworkProvidersConfigYamlSerializer.Serialize(config);
    }

    public static bool IsAuthorable(ConfigDomain domain) => Authorable.ContainsKey(domain);

    /// <summary>
    /// Whether an authorable domain can be authored at a non-default scope (env/tag/host). StorageConfig
    /// is per-host/environment (paths differ per machine), so it is scoped; NetworkProviders is the
    /// single global network topology the controller realizes once, so only the default scope is
    /// meaningful — its source and the controller consumers read the default-scope value only. The same
    /// holds for Environments: an environment is defined once for the whole deployment, and scoping it
    /// would let a component disagree about which environments exist or where they live.
    /// </summary>
    public static bool SupportsScopedAuthoring(ConfigDomain domain) => domain == ConfigDomain.StorageConfig;

    /// <summary>
    /// Validates the payload against the domain's schema and returns its canonical serialization.
    /// Returns false when the domain is not authorable or the payload is invalid.
    /// </summary>
    public static bool TryCanonicalize(ConfigDomain domain, string payload, out string canonical) =>
        TryCanonicalize(domain, payload, out canonical, out _);

    /// <summary>
    /// As <see cref="TryCanonicalize(ConfigDomain,string,out string)"/>, also returning the specific
    /// validation error (e.g. the network-provider validation detail) so it can be surfaced to the
    /// operator instead of a generic message.
    /// </summary>
    public static bool TryCanonicalize(ConfigDomain domain, string payload, out string canonical, out string? error)
    {
        canonical = payload;
        error = null;

        if (!Authorable.TryGetValue(domain, out var canonicalize))
        {
            error = $"The {domain} domain is system-derived and cannot be authored.";
            return false;
        }

        // A blank/whitespace payload is no input at all — reject it. A payload that PARSES to an empty
        // config (e.g. `{}`) is a valid "default-only vocabulary" (the same value the settings fallback
        // produces for a host with no named datastores), so it is accepted; the empty vocabulary is a
        // legitimate, if destructive, authoring choice, not a silent accident.
        if (string.IsNullOrWhiteSpace(payload))
        {
            error = "The configuration payload is empty.";
            return false;
        }

        try
        {
            canonical = canonicalize(payload);
            return true;
        }
        catch (InvalidConfigException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            // The canonicalizer validates untrusted operator input; any other failure (a serializer edge
            // case, a null document) is still "this payload is invalid", not a reason to crash the
            // handler with an unhandled bus exception. Report it as a validation error.
            error = $"The {domain} configuration could not be parsed: {ex.Message}";
            return false;
        }
    }
}
