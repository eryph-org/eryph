using System;
using Eryph.Messages.Operations.Commands;

namespace Eryph.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class RemoveVMCommand : IVMCommand
    {
        public Guid MachineId { get; set; }
        public Guid VMId { get; set; }
    }

    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class RemoveVirtualDiskCommand: IHostAgentCommand
    {
        public Guid DiskId { get; set; }
        public string Path { get; set; }
        public string FileName { get; set; }
        public string AgentName { get; set; }
    }
}