using System;
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
/// capability). The validator deserializes with unmapped members disallowed, so a payload that
/// validates here deserializes to the same object the realizer applies — rejecting invalid JSON,
/// wrong-cased/foreign members and a bare <c>null</c> before any of them can be distributed and wedge
/// or silently empty the fleet.
/// </remarks>
internal static class ConfigDomainDescriptors
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly IReadOnlyDictionary<ConfigDomain, Func<string, bool>> Authorable =
        new Dictionary<ConfigDomain, Func<string, bool>>
        {
            [ConfigDomain.PlacementConfig] = payload => CanParse<PlacementConfig>(payload),
        };

    public static bool IsAuthorable(ConfigDomain domain) => Authorable.ContainsKey(domain);

    public static bool IsValidPayload(ConfigDomain domain, string payload) =>
        Authorable.TryGetValue(domain, out var validate) && validate(payload);

    private static bool CanParse<T>(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payload, StrictJson) is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
