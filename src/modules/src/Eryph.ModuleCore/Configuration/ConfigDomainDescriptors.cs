using System;
using System.Collections.Generic;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Components;
using LanguageExt;

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
            [ConfigDomain.StorageConfig] = payload =>
                StorageConfigYamlSerializer.Serialize(StorageConfigYamlSerializer.Deserialize(payload)),

            [ConfigDomain.NetworkProviders] = CanonicalizeNetworkProviders,
        };

    // Network provider config has richer semantic rules than the shape check the serializer does
    // (overlapping NAT subnets, per-type field restrictions, IP-pool bounds), so validate explicitly.
    // The IP-pool next-IP cursor is runtime allocation state, not authored config, so strip it — the
    // controller keeps the cursor in its own state, and authored versions must not churn on allocation.
    private static string CanonicalizeNetworkProviders(string payload)
    {
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
    /// Validates the payload against the domain's schema and returns its canonical serialization.
    /// Returns false when the domain is not authorable or the payload is invalid.
    /// </summary>
    public static bool TryCanonicalize(ConfigDomain domain, string payload, out string canonical)
    {
        canonical = payload;
        if (!Authorable.TryGetValue(domain, out var canonicalize))
            return false;

        // An empty document deserializes to an empty config; require the operator to actually provide
        // one so a blank submission cannot silently distribute an empty vocabulary.
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            canonical = canonicalize(payload);
            return true;
        }
        catch (InvalidConfigException)
        {
            return false;
        }
    }
}
