using System;
using System.Collections.Generic;
using System.Text.Json;
using Eryph.Messages.Components;

namespace Eryph.StateDb.Model;

/// <summary>
/// The controller's record of a component that has registered as part of the
/// deployment — the durable, cross-process service catalog. Identity is the
/// stable <see cref="ComponentId"/> (not the machine name). Applied config
/// versions are stored as a JSON column following the same pattern as
/// <see cref="CatletMetadata"/>.
/// </summary>
public class ComponentRegistration
{
    public Guid Id { get; set; }

    public Guid ComponentId { get; set; }

    public ComponentType ComponentType { get; set; }

    public Guid InstanceId { get; set; }

    public required string MachineName { get; set; }

    public string? Version { get; set; }

    public required string InboundQueue { get; set; }

    public ComponentRegistrationStatus Status { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset LastHeartbeat { get; set; }

    internal string AppliedConfigVersionsJson
    {
        get => JsonSerializer.Serialize(AppliedConfigVersions);
        set => AppliedConfigVersions = string.IsNullOrEmpty(value)
            ? new Dictionary<ConfigDomain, long>()
            : JsonSerializer.Deserialize<Dictionary<ConfigDomain, long>>(value)
              ?? new Dictionary<ConfigDomain, long>();
    }

    /// <summary>The config version this component has applied per domain.</summary>
    public Dictionary<ConfigDomain, long> AppliedConfigVersions { get; set; } = new();

    internal string AdvertisedEndpointsJson
    {
        get => JsonSerializer.Serialize(AdvertisedEndpoints);
        set => AdvertisedEndpoints = string.IsNullOrEmpty(value)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(value)
              ?? new Dictionary<string, string>();
    }

    /// <summary>Service endpoints this component hosts and advertises (logical name → URL).</summary>
    public Dictionary<string, string> AdvertisedEndpoints { get; set; } = new();
}
