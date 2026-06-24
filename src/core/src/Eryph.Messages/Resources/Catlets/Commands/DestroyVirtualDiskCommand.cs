using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class DestroyVirtualDiskCommand : IHasResource, ICommandWithName
{
    public Guid DiskId { get; set; }

    public string GetCommandName() => "Destroy Virtual Disk";

    public Resource Resource => new(ResourceType.VirtualDisk, DiskId);
}
