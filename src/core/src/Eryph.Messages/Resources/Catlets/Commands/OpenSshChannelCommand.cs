using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class OpenSshChannelCommand : IHasResource, ICommandWithName
{
    public Guid CatletId { get; set; }

    // External-pool KVP values authorizing the operator's key, built by the
    // endpoint. Empty for the pre-injected-key flow (no write).
    public Dictionary<string, string> AccessKeyValues { get; set; } = new();
    public Resource Resource => new(ResourceType.Catlet, CatletId);
    public string GetCommandName() => "Opening SSH channel";
}
