using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Eryph.Core;
using Eryph.Messages.Components;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Describes which configuration domains an operator may author via the management API and how to
/// validate a proposed payload. Domains not listed here are system-derived (built from controller
/// state by their <c>IConfigSource</c>) and must not be authored — accepting one would persist a value
/// no source ever reads and misrepresent the effective configuration.
/// </summary>
/// <remarks>
/// This is the minimal form of the per-domain descriptor in plan §10.7 (which also carries the scoping
/// capability). Validation deserializes with unmapped members disallowed — so an invalid,
/// wrong-cased/foreign-member or bare-<c>null</c> payload is rejected before it can be distributed and
/// wedge or silently empty the fleet — and returns the <b>canonical</b> form (re-serialized), so two
/// semantically-identical payloads that differ only in whitespace or property order do not create noisy
/// versions or redundant redistributions.
/// </remarks>
internal static class ConfigDomainDescriptors
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private delegate bool TryCanonicalizeDelegate(string payload, out string canonical);

    private static readonly IReadOnlyDictionary<ConfigDomain, TryCanonicalizeDelegate> Authorable =
        new Dictionary<ConfigDomain, TryCanonicalizeDelegate>
        {
            [ConfigDomain.PlacementConfig] = Canonicalize<PlacementConfig>,
        };

    public static bool IsAuthorable(ConfigDomain domain) => Authorable.ContainsKey(domain);

    /// <summary>
    /// Validates the payload against the domain's schema and returns its canonical serialization.
    /// Returns false when the domain is not authorable or the payload is invalid.
    /// </summary>
    public static bool TryCanonicalize(ConfigDomain domain, string payload, out string canonical)
    {
        canonical = payload;
        return Authorable.TryGetValue(domain, out var canonicalize) && canonicalize(payload, out canonical);
    }

    private static bool Canonicalize<T>(string payload, out string canonical)
    {
        canonical = payload;
        try
        {
            var value = JsonSerializer.Deserialize<T>(payload, StrictJson);
            if (value is null)
                return false;

            // Re-serialize with the default options the realizer/source use, so the stored payload is
            // canonical (stable whitespace and property order).
            canonical = JsonSerializer.Serialize(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
