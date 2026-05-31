using System;
using Eryph.Messages.Components;

namespace Eryph.StateDb.Model;

/// <summary>
/// The controller's authoritative copy of one cluster-configuration domain,
/// carrying a monotonic <see cref="Version"/>. <see cref="Payload"/> is the
/// serialized snapshot distributed to entitled components. There is at most one
/// record per <see cref="ConfigDomain"/>.
/// </summary>
public class ConfigRecord
{
    public Guid Id { get; set; }

    public ConfigDomain Domain { get; set; }

    public long Version { get; set; }

    public required string Payload { get; set; }

    public DateTimeOffset LastUpdated { get; set; }
}
