using System;
using System.Collections.Generic;

namespace Eryph.Messages.Components;

/// <summary>
/// Assigns operator-owned targeting metadata (environment + tags) to a registered component —
/// dispatched as an operation from the management API. The metadata is not reported by the component;
/// it is how the operator groups hosts so scoped configuration can be targeted at them.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class SetComponentMetadataCommand : IHasCorrelationId
{
    public Guid CorrelationId { get; set; }

    public Guid ComponentId { get; set; }

    /// <summary>The environment to assign, or null to clear it.</summary>
    public string? Environment { get; set; }

    /// <summary>The complete replacement tag set (key → value); a null set is treated as no tags, and a
    /// null value as an empty one (the value type is nullable because a deserialized message can carry
    /// null values).</summary>
    public Dictionary<string, string?>? Tags { get; set; } = new();
}
