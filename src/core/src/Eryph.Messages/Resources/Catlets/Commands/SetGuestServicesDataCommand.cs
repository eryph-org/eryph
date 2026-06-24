using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

// Generic guest-services write: sets External-pool KVP values on the guest
// and/or removes keys. The endpoint builds these and supplies an operation
// name for the operations log.
[SendMessageTo(MessageRecipient.Controllers)]
public class SetGuestServicesDataCommand : IHasResource, ICommandWithName
{
    public Guid CatletId { get; set; }
    public string OperationName { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();
    public List<string> RemoveKeys { get; set; } = [];

    public string GetCommandName() =>
        string.IsNullOrEmpty(OperationName) ? "Setting guest services data" : OperationName;

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
