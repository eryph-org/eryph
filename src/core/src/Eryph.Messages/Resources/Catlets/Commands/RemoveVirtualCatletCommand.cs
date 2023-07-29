using Eryph.Resources;
using System;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class RemoveVirtualCatletCommand : IVMCommand, IHasResource
{
    public Guid CatletId { get; set; }
    public Guid VMId { get; set; }
    public Resource Resource => new(ResourceType.Catlet, CatletId);

}