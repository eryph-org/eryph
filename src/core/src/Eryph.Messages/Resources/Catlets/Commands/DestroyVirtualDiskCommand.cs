using Eryph.Resources;
using System;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class DestroyVirtualDiskCommand : IHasResource, ICommandWithName
    {
        public Guid DiskId { get; set; }
        public Resource Resource => new(ResourceType.VirtualDisk, DiskId);
        public string GetCommandName() => "Destroy Virtual Disk";
    }
}