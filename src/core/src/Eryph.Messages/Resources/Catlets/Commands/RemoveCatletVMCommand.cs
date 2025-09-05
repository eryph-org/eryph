using Eryph.Resources;
using System;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class RemoveCatletVMCommand : IVMCommand, IHasResource
{
    public Guid CatletId { get; set; }
    public Guid VmId { get; set; }
    public Resource Resource => new(ResourceType.Catlet, CatletId);

}