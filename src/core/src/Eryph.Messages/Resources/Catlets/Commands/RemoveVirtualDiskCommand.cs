using System;
using Eryph.Resources;
using LanguageExt.Pipes;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class RemoveVirtualDiskCommand: IHostAgentCommand, IHasResource
    {
        public Guid DiskId { get; set; }
        public string Path { get; set; }
        public string FileName { get; set; }
        public string AgentName { get; set; }
        public Resource Resource => new(ResourceType.VirtualDisk, DiskId);

    }
}