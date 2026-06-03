using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

// Sets External-pool KVP values on the guest and/or removes keys.
[SendMessageTo(MessageRecipient.VMHostAgent)]
public class SetGuestServicesDataVMCommand : IVMCommand, IHasResource
{
    public Guid CatletId { get; set; }
    public Guid VmId { get; set; }
    public Dictionary<string, string> Values { get; set; } = new();
    public List<string> RemoveKeys { get; set; } = new();
    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
