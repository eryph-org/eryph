using System;
using System.Collections.Generic;

namespace Eryph.Messages.Components;

/// <summary>
/// The controller's initial (or delta) configuration snapshot for a component,
/// routed directly to the component's inbound queue. Carries one bundle per
/// configuration domain the component is entitled to and is missing.
/// </summary>
public class ConfigSnapshotCommand
{
    public Guid ComponentId { get; set; }

    public List<ConfigBundle> Bundles { get; set; } = new();
}
