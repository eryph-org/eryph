namespace Eryph.Messages.Components;

/// <summary>
/// Requests a configuration domain's current authored value — the read side of the config-management
/// API. The controller replies with a <see cref="ConfigDomainResponse"/>.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class GetConfigDomainCommand
{
    public ConfigDomain Domain { get; set; }
}
