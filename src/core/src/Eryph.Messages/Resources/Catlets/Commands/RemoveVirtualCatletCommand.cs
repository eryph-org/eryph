using System;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class RemoveVirtualCatletCommand : IVMCommand
{
    public Guid CatletId { get; set; }
    public Guid VMId { get; set; }
}