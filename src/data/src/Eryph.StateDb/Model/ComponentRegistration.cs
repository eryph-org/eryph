using System;
using System.Collections.Generic;
using System.Text.Json;
using Eryph.Messages.Components;

namespace Eryph.StateDb.Model;

/// <summary>
/// The controller's record of a component that has registered as part of the
/// deployment — the durable, cross-process service catalog. Identity is the
/// stable <see cref="ComponentId"/> (not the machine name). Applied config
/// versions and advertised capabilities are stored as JSON columns following the
/// same pattern as <see cref="CatletMetadata"/>.
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

    internal string CapabilitiesJson
    {
        get => JsonSerializer.Serialize(Capabilities);
        set => Capabilities = string.IsNullOrEmpty(value)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(value)
              ?? new Dictionary<string, string>();
    }

    /// <summary>Derived capabilities the component advertised at registration.</summary>
    public Dictionary<string, string> Capabilities { get; set; } = new();
}
