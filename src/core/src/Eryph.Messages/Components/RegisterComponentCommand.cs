using System;
using System.Collections.Generic;

namespace Eryph.Messages.Components;

/// <summary>
/// Sent by a component to the controller on startup to register as alive and
/// announce the configuration versions it already holds. The controller persists
/// the registration; the component pulls any missing configuration separately via
/// <see cref="RequestConfigCommand"/> (registration itself carries no config reply).
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

    /// <summary>Config versions the component already has, so the controller can send only deltas.</summary>
    public Dictionary<ConfigDomain, long> KnownConfigVersions { get; set; } = new();

    /// <summary>
    /// Service endpoints this component hosts and advertises to the deployment (logical
    /// name → URL), e.g. the identity component advertising <c>identity</c>. The controller
    /// aggregates these into the <see cref="ConfigDomain.Endpoints"/> map, where an operator
    /// override always wins over an advertised value. Empty for components that host nothing.
    /// </summary>
    public Dictionary<string, string> AdvertisedEndpoints { get; set; } = new();
}
