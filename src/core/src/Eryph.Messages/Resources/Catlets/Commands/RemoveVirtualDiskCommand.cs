using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class RemoveVirtualDiskCommand : IHostAgentCommand, IHasResource
{
    public Guid DiskId { get; set; }
    public string Path { get; set; }
    public string FileName { get; set; }
    public Resource Resource => new(ResourceType.VirtualDisk, DiskId);
    public string AgentName { get; set; }
}
