using System;
using Haipa.Messages.Operations.Commands;

namespace Haipa.Messages.Resources.Machines.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class RemoveVMCommand : IVMCommand
    {
        public Guid MachineId { get; set; }
        public Guid VMId { get; set; }
    }
}