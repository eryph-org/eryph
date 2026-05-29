namespace Eryph.Messages.Components;

/// <summary>
/// Triggers the controller to re-evaluate a configuration domain and, if its
/// content changed, bump the version and push the new bundle to subscribing live
/// components. Sent when the controller-owned source of a domain changes.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class RefreshConfigDomainCommand
{
    public ConfigDomain Domain { get; set; }
}
