using System;
using System.Collections.Generic;

namespace Eryph.Messages.Components;

/// <summary>
/// Sent by a component on startup to request its current configuration from the
/// controller — the pull side of distribution. The controller replies (to the
/// request's return address) with a <see cref="ConfigSnapshotCommand"/> containing the
/// domains the component is entitled to and does not already hold (per
/// <see cref="KnownConfigVersions"/>). The destination is the request's return address,
/// not a field on this message, so a snapshot cannot be redirected to an arbitrary queue.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class RequestConfigCommand
{
    public Guid ComponentId { get; set; }

    public ComponentType ComponentType { get; set; }

    public Dictionary<ConfigDomain, long> KnownConfigVersions { get; set; } = new();
}
