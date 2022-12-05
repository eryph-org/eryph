using System;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class RemoveVirtualDiskCommand: IHostAgentCommand
    {
        public Guid DiskId { get; set; }
        public string Path { get; set; }
        public string FileName { get; set; }
        public string AgentName { get; set; }
    }
}