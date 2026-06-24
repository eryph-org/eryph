using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class RemoveCatletVMCommand : IVMCommand, IHasResource
{
    public Resource Resource => new(ResourceType.Catlet, CatletId);
    public Guid CatletId { get; set; }
    public Guid VmId { get; set; }
}
