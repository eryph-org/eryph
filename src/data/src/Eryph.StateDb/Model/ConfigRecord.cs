using System;
using Eryph.Messages.Components;

namespace Eryph.StateDb.Model;

/// <summary>
/// The controller's materialized copy of one cluster-configuration domain at one scope,
/// carrying a monotonic <see cref="Version"/>. <see cref="Payload"/> is the serialized snapshot
/// distributed to components that resolve this <see cref="Scope"/>. There is at most one record
/// per (<see cref="Domain"/>, <see cref="Scope"/>).
/// </summary>
public class ConfigRecord
{
    public Guid Id { get; set; }

    public ConfigDomain Domain { get; set; }

    /// <summary>The scope selector this materialized value targets (empty = default/match-all).</summary>
    public required string Scope { get; set; }

    public long Version { get; set; }

    public required string Payload { get; set; }

    public DateTimeOffset LastUpdated { get; set; }
}
