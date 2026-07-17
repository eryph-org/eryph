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

    /// <summary>The name of the site the component is located in, or null to leave it unchanged.
    /// Unlike <see cref="Environment"/> it cannot be cleared: a component always runs somewhere.
    /// A name rather than an id, because that is what an operator declares and can therefore refer
    /// to; the handler resolves it and rejects a site which does not exist.</summary>
    public string? Site { get; set; }

    /// <summary>The complete replacement tag set (key → value). A null set is treated as no tags; a null
    /// value as an empty one. The value type is nullable because this message is populated by
    /// deserialization, which can yield null values regardless of the domain intent.</summary>
    public Dictionary<string, string?>? Tags { get; set; } = new();
}
