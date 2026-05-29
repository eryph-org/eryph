using System;
using System.Collections.Generic;

namespace Eryph.Messages.Components;

/// <summary>
/// Sent by a component to the controller on startup to register as alive and
/// announce the configuration versions it already holds. The controller persists
/// the registration and replies with the configuration the component is missing.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class RegisterComponentCommand
{
    /// <summary>Stable identity of the component (not the machine name).</summary>
    public Guid ComponentId { get; set; }

    public ComponentType ComponentType { get; set; }

    /// <summary>Identifies this run of the component; changes on restart.</summary>
    public Guid InstanceId { get; set; }

    public string MachineName { get; set; } = "";

    public string Version { get; set; } = "";

    /// <summary>The bus queue the controller routes configuration to.</summary>
    public string InboundQueue { get; set; } = "";

    /// <summary>Derived capabilities the component advertises (e.g. datastores, switches).</summary>
    public Dictionary<string, string> Capabilities { get; set; } = new();

    /// <summary>Config versions the component already has, so the controller can send only deltas.</summary>
    public Dictionary<ConfigDomain, long> KnownConfigVersions { get; set; } = new();
}
