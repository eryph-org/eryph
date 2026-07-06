namespace Eryph.Messages.Components;

/// <summary>
/// Sets a new operator-authored version of a configuration domain — the write side of the
/// config-management API. The payload is the domain's serialized configuration; the controller stores
/// it as a new version (preserving history) and re-distributes it to entitled components.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class SetConfigDomainCommand
{
    public ConfigDomain Domain { get; set; }

    public string? Payload { get; set; }

    /// <summary>The authoring principal, for audit/UI.</summary>
    public string? Author { get; set; }
}
