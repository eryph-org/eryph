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
}