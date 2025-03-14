using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class StopVMCommand : IVMCommand, IHasResource
{
    public Guid CatletId { get; set; }

    public Guid VMId { get; set; }
    
    public Resource Resource => new(ResourceType.Catlet, CatletId);

    public bool StopProcess { get; set; }
}
